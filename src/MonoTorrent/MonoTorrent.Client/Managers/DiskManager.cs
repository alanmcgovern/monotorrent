using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent.Client.Managers
{
    public class DiskManager : IDisposable
    {
        private static MainLoop IOLoop = new MainLoop("Disk IO");

        #region Member Variables

        private object bufferLock = new object();
        private Queue<BufferedIO> bufferedReads;
        private Queue<BufferedIO> bufferedWrites;
        private bool disposed;
        private ClientEngine engine;
        private MainLoopTask LoopTask;

        private SpeedMonitor readMonitor;
        private SpeedMonitor writeMonitor;

        internal RateLimiter readLimiter;
        internal RateLimiter writeLimiter;
        private PieceWriter writer;

        #endregion Member Variables


        #region Properties

        public bool Disposed
        {
            get { return disposed; }
        }

        public int QueuedWrites
        {
            get { return this.bufferedWrites.Count; }
        }

        public int ReadRate
        {
            get { return readMonitor.Rate; }
        }

        public int WriteRate
        {
            get { return writeMonitor.Rate; }
        }

        public long TotalRead
        {
            get { return readMonitor.Total; }
        }

        public long TotalWritten
        {
            get { return writeMonitor.Total; }
        }

        internal PieceWriter Writer
        {
            get { return writer; }
        }

        #endregion Properties


        #region Constructors

        internal DiskManager(ClientEngine engine, PieceWriter writer)
        {
            this.bufferedReads = new Queue<BufferedIO>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.engine = engine;
            this.readLimiter = new RateLimiter();
            this.readMonitor = new SpeedMonitor();
            this.writeMonitor = new SpeedMonitor();
            this.writeLimiter = new RateLimiter();
            this.writer = writer;

            LoopTask = delegate {
                if (disposed)
                    return;

                while (this.bufferedWrites.Count > 0 && (engine.Settings.MaxWriteRate == 0 || writeLimiter.Chunks > 0))
                {
                    BufferedIO write;
                    lock (bufferLock)
                        write = this.bufferedWrites.Dequeue();
                    Interlocked.Add(ref writeLimiter.Chunks, -write.buffer.Count / ConnectionManager.ChunkLength);
                    PerformWrite(write);
                }

                while (this.bufferedReads.Count > 0 && (engine.Settings.MaxReadRate == 0 || readLimiter.Chunks > 0))
                {
                    BufferedIO read;
                    lock(bufferLock)
                        read = this.bufferedReads.Dequeue();
                    Interlocked.Add(ref readLimiter.Chunks, -read.Count / ConnectionManager.ChunkLength);
                    PerformRead(read);
                }
            };

            IOLoop.QueueTimeout(TimeSpan.FromSeconds(1), delegate {
                if (disposed)
                    return false;

                readMonitor.Tick();
                writeMonitor.Tick();
                return true;
            });
        }

        #endregion Constructors


        #region Methods

        internal WaitHandle CloseFileStreams(string path, TorrentFile[] files)
        {
            ManualResetEvent handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate {
                // Dump all buffered reads for the manager we're closing the streams for
                List<BufferedIO> writes = new List<BufferedIO>();
                lock (bufferLock)
                {
                    List<BufferedIO> list = new List<BufferedIO>(bufferedReads);
                    list.RemoveAll(delegate(BufferedIO io) { return io.Files == files; });
                    bufferedReads = new Queue<BufferedIO>(list);

                    list.Clear();
                    list.AddRange(bufferedWrites);

                    for (int i = 0; i < list.Count; i++)
                        if (list[i].Files == files)
                            writes.Add(list[i]);

                    list.RemoveAll(delegate(BufferedIO io) { return io.Files == files; });
                    bufferedWrites = new Queue<BufferedIO>(list);
                }

                // Process all remaining writes
                foreach (BufferedIO io in writes)
                    if (io.Files == files)
                        PerformWrite(io);
                writer.Close(path, files);

                handle.Set();
            });

            return handle;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            // FIXME: Ensure everything is written to disk before killing the mainloop.
            IOLoop.QueueWait((MainLoopTask)writer.Dispose);
        }

        public void Flush()
        {
            IOLoop.QueueWait(delegate {
                foreach (TorrentManager manager in engine.Torrents)
                    writer.Flush(manager.SavePath, manager.Torrent.Files);
            });
        }

        public void Flush(TorrentManager manager)
        {
            Check.Manager(manager);
            IOLoop.QueueWait(delegate {
                writer.Flush(manager.SavePath, manager.Torrent.Files);
            });
        }

        private void PerformWrite(BufferedIO data)
        {
            PeerId id = data.Id;
            Piece piece = data.Piece;

            // Find the block that this data belongs to and set it's state to "Written"
            int index = data.PieceOffset / Piece.BlockSize;

            // Perform the actual write
            writer.Write(data);
            writeMonitor.AddDelta(data.Count);

            piece.Blocks[index].Written = true;

            if (data.WaitHandle != null)
                data.WaitHandle.Set();

            // If we haven't written all the pieces to disk, there's no point in hash checking
            if (!piece.AllBlocksWritten)
                return;

            // Hashcheck the piece as we now have all the blocks.
            byte[] hash = id.Engine.DiskManager.GetHash(id.TorrentManager, piece.Index);
            bool result = id.TorrentManager.Torrent.Pieces.IsValid(hash, piece.Index);
            id.TorrentManager.Bitfield[data.PieceIndex] = result;

            ClientEngine.MainLoop.Queue(delegate {
                id.TorrentManager.PieceManager.UnhashedPieces[piece.Index] = false;

                id.TorrentManager.HashedPiece(new PieceHashedEventArgs(id.TorrentManager, piece.Index, result));
                List<PeerId> peers = new List<PeerId>(piece.Blocks.Length);
                for (int i = 0; i < piece.Blocks.Length; i++)
                    if (piece.Blocks[i].RequestedOff != null && !peers.Contains(piece.Blocks[i].RequestedOff))
                        peers.Add(piece.Blocks[i].RequestedOff);

                for (int i = 0; i < peers.Count; i++)
                    if (peers[i].Connection != null)
                        id.Peer.HashedPiece(result);

                // If the piece was successfully hashed, enqueue a new "have" message to be sent out
                if (result)
                    id.TorrentManager.finishedPieces.Enqueue(piece.Index);
            });
        }

        private void PerformRead(BufferedIO io)
        {
            io.ActualCount = writer.ReadChunk(io);
            readMonitor.AddDelta(io.ActualCount);

            if (io.WaitHandle != null)
                io.WaitHandle.Set();
        }

        internal int Read(TorrentManager manager, byte[] buffer, int bufferOffset, long pieceStartIndex, int bytesToRead)
        {
            string path = manager.SavePath;
            ArraySegment<byte> b = new ArraySegment<byte>(buffer, bufferOffset, bytesToRead);
            BufferedIO io = new BufferedIO(b, pieceStartIndex, bytesToRead, manager.Torrent.PieceLength, manager.Torrent.Files, path);
            IOLoop.QueueWait((MainLoopTask)delegate {
                PerformRead(io);
            });
            return io.ActualCount;
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(delegate {
                foreach (TorrentFile file in manager.Torrent.Files)
                    if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                        writer.Flush(manager.SavePath, file);
            });
        }

        internal void QueueRead(BufferedIO io)
        {
            if (Thread.CurrentThread == IOLoop.thread && io.WaitHandle != null)
                PerformRead(io);
            else
                lock (bufferLock)
                {
                    bufferedReads.Enqueue(io);
                    DiskManager.IOLoop.Queue(LoopTask);
                }
        }

        internal void QueueWrite(BufferedIO io)
        {
            if (Thread.CurrentThread == IOLoop.thread && io.WaitHandle != null)
                PerformWrite(io);
            else
                lock (bufferLock)
                {
                    bufferedWrites.Enqueue(io);
                    DiskManager.IOLoop.Queue(LoopTask);
                }
        }

        internal bool CheckFilesExist(TorrentManager manager)
        {
            return writer.Exists(manager.SavePath, manager.Torrent.Files);
        }

        internal byte[] GetHash(TorrentManager manager, int pieceIndex)
        {
            long fileSize = manager.Torrent.Size;
            int pieceLength = manager.Torrent.PieceLength;
            int bytesToRead = 0;
            long pieceStartIndex = (long)pieceLength * pieceIndex;
            BufferedIO io = null;
            ArraySegment<byte> hashBuffer = BufferManager.EmptyBuffer;
            List<BufferedIO> list = new List<BufferedIO>();

            for (long i = pieceStartIndex; i < (pieceStartIndex + pieceLength); i += Piece.BlockSize)
            {
                hashBuffer = BufferManager.EmptyBuffer;
                ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);
                bytesToRead = Piece.BlockSize;
                if ((i + bytesToRead) > fileSize)
                    bytesToRead = (int)(fileSize - i);

                io = new BufferedIO(hashBuffer, i, bytesToRead, manager.Torrent.PieceLength, manager.Torrent.Files, manager.SavePath);
                io.WaitHandle = new ManualResetEvent(false);
                list.Add(io);
                manager.Engine.DiskManager.QueueRead(io);

                if (bytesToRead != Piece.BlockSize)
                    break;
            }

            using (SHA1 hasher = SHA1.Create())
            {
                hasher.Initialize();
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].WaitHandle.WaitOne();
                    list[i].WaitHandle.Close();
                    hashBuffer = list[i].buffer;
                    hasher.TransformBlock(hashBuffer.Array, hashBuffer.Offset, list[i].ActualCount, hashBuffer.Array, hashBuffer.Offset);
                    ClientEngine.BufferManager.FreeBuffer(ref list[i].buffer);
                }
                hasher.TransformFinalBlock(hashBuffer.Array, hashBuffer.Offset, 0);
                return hasher.Hash;
            }
        }

        #endregion

        internal void MoveFiles(TorrentManager torrentManager, string oldPath, string newPath, bool overWriteExisting)
        {
            IOLoop.QueueWait(delegate {
                    writer.Move(oldPath, newPath, torrentManager.Torrent.Files, overWriteExisting);
            });
        }
    }
}
