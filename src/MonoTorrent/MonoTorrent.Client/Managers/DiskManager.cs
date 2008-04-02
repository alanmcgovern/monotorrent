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
        private class PieceFlush
        {
            public TorrentManager Manager;
            public int PieceIndex;

            public PieceFlush(TorrentManager manager, int pieceIndex)
            {
                Manager = manager;
                PieceIndex = pieceIndex;
            }
        }
        private class StreamClose
        {
            public ManualResetEvent Handle;
            public TorrentManager Manager;

            public StreamClose(ManualResetEvent handle, TorrentManager manager)
            {
                Handle = handle;
                Manager = manager;
            }
        }

        #region Member Variables

        Queue<PieceFlush> flushQueue;
        Queue<StreamClose> closeStreams;
        Queue<BufferedIO> bufferedReads;
        Queue<BufferedIO> bufferedWrites;
        private ClientEngine engine;

        private SpeedMonitor readMonitor;
        private SpeedMonitor writeMonitor;

        internal RateLimiter readLimiter;
        internal RateLimiter writeLimiter;
        private PieceWriter writer;

        #endregion Member Variables


        #region Old Variables

        private bool ioActive;                                  // Used to signal when the IO thread is running
        private Thread ioThread;                                // The dedicated thread used for reading/writing
        private object queueLock;                               // Used to synchronise access on the IO thread
        internal ReaderWriterLock streamsLock;
        private ManualResetEvent threadWait;                    // Used to signal the IO thread when some data is ready for it to work on

        #endregion Old Variables


        #region Properties

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
            this.closeStreams = new Queue<StreamClose>();
            this.flushQueue = new Queue<PieceFlush>();
            this.engine = engine;
            this.ioActive = true;
            this.ioThread = new Thread(new ThreadStart(RunIO));
            this.queueLock = new object();
            this.readLimiter = new RateLimiter();
            this.readMonitor = new SpeedMonitor();
            this.streamsLock = new ReaderWriterLock();
            this.threadWait = new ManualResetEvent(false);
            this.writeMonitor = new SpeedMonitor();
            this.writeLimiter = new RateLimiter();
            this.writer = writer;
            this.ioThread.Start();
        }

        #endregion Constructors


        #region Methods

        internal WaitHandle CloseFileStreams(TorrentManager manager)
        {
            lock (queueLock)
            {
                ManualResetEvent handle = new ManualResetEvent(false);
                closeStreams.Enqueue(new StreamClose(handle, manager));
                return handle;
            }
        }


        public void Dispose()
        {
            ioActive = false;
            this.threadWait.Set();
            this.ioThread.Join();
            this.writer.Dispose();
        }


        /// <summary>
        /// Performs the buffered write
        /// </summary>
        /// <param name="bufferedFileIO"></param>
        private void PerformWrite(BufferedIO data)
        {
            PeerIdInternal id = data.Id;
            ArraySegment<byte> recieveBuffer = data.buffer;
            Piece piece = data.Piece;

            // Find the block that this data belongs to and set it's state to "Written"
            int index = data.PieceOffset / Piece.BlockSize;

            // Perform the actual write
            lock (writer)
            {
                writer.Write(data);
                writeMonitor.AddDelta(data.Count);
            }
            piece.Blocks[index].Written = true;
            id.TorrentManager.FileManager.RaiseBlockWritten(new BlockEventArgs(data));

            // Release the buffer back into the buffer manager.
            //ClientEngine.BufferManager.FreeBuffer(ref bufferedFileIO.Buffer);
#warning FIX THIS - don't free the buffer here anymore

            // If we haven't written all the pieces to disk, there's no point in hash checking
            if (!piece.AllBlocksWritten)
                return;

            // Hashcheck the piece as we now have all the blocks.
            bool result = id.TorrentManager.Torrent.Pieces.IsValid(id.TorrentManager.FileManager.GetHash(piece.Index, false), piece.Index);
            id.TorrentManager.Bitfield[data.PieceIndex] = result;
            lock (id.TorrentManager.PieceManager.UnhashedPieces)
                id.TorrentManager.PieceManager.UnhashedPieces[piece.Index] = false;

            id.TorrentManager.HashedPiece(new PieceHashedEventArgs(id.TorrentManager, piece.Index, result));
            List<PeerIdInternal> peers = new List<PeerIdInternal>(piece.Blocks.Length);
            for (int i = 0; i < piece.Blocks.Length; i++)
                if (piece.Blocks[i].RequestedOff != null && !peers.Contains(piece.Blocks[i].RequestedOff))
                    peers.Add(piece.Blocks[i].RequestedOff);

            for (int i = 0; i < peers.Count; i++)
                lock (peers[i])
                    if (peers[i].Connection != null)
                        id.Peer.HashedPiece(result);

            // If the piece was successfully hashed, enqueue a new "have" message to be sent out
            if (result)
                lock (id.TorrentManager.finishedPieces)
                    id.TorrentManager.finishedPieces.Enqueue(piece.Index);
        }


        /// <summary>
        /// Performs the buffered read
        /// </summary>
        /// <param name="bufferedFileIO"></param>
        private void PerformRead(BufferedIO io)
        {
            lock (writer)
            {
                io.ActualCount = writer.ReadChunk(io);
                readMonitor.AddDelta(io.ActualCount);
            }
            io.WaitHandle.Set();
        }


        internal int Read(TorrentManager manager, byte[] buffer, int bufferOffset, long pieceStartIndex, int bytesToRead)
        {
            lock (writer)
            {
                readMonitor.AddDelta(bytesToRead);
                ArraySegment<byte> b = new ArraySegment<byte>(buffer, bufferOffset, bytesToRead);
                return writer.ReadChunk(new BufferedIO(b, pieceStartIndex, bytesToRead, manager));
            }
        }

        /// <summary>
        /// Queues a block of data to be written asynchronously
        /// </summary>
        /// <param name="id">The peer who sent the block</param>
        /// <param name="recieveBuffer">The array containing the block</param>
        /// <param name="message">The PieceMessage</param>
        /// <param name="piece">The piece that the block to be written is part of</param>
        internal void QueueWrite(BufferedIO data)
        {
            lock (this.queueLock)
            {
                if (Thread.CurrentThread == this.ioThread)
                {
                    PerformWrite(data);
                }
                else
                {
                    bufferedWrites.Enqueue(data);
                    SetHandleState(true);
                }
            }
        }


        internal void QueueRead(BufferedIO io)
        {
            lock (this.queueLock)
            {
                if (Thread.CurrentThread == this.ioThread)
                {
                    PerformRead(io);
                }
                else
                {
                    bufferedReads.Enqueue(io);
                    SetHandleState(true);
                }
            }
        }


        /// <summary>
        /// This method runs in a dedicated thread. It performs all the async reads and writes as they are queued
        /// </summary>
        private void RunIO()
        {
            try
            {
                BufferedIO write;
                BufferedIO read;
                PieceFlush flush;

                while (ioActive || this.bufferedWrites.Count > 0 || this.closeStreams.Count > 0)
                {
                    flush = null;
                    write = null;
                    read = null;

                    // Take a lock on the queue and dequeue any reads/writes that are available. Then lose the lock before
                    // performing the actual read/write to avoid blocking other threads
                    lock (this.queueLock)
                    {
                        if (this.closeStreams.Count > 0)
                        {
                            StreamClose close = closeStreams.Dequeue();

                            try
                            {
                                // Dump all buffered reads for the manager we're closing the streams for
                                List<BufferedIO> list = new List<BufferedIO>(bufferedReads);
                                list.RemoveAll(delegate(BufferedIO io) { return io.Manager == close.Manager; });
                                bufferedReads = new Queue<BufferedIO>(list);

                                // Process all remaining reads
                                list = new List<BufferedIO>(bufferedWrites);
                                foreach (BufferedIO io in list)
                                    if (io.Manager == close.Manager)
                                        PerformWrite(io);
                                writer.Close(close.Manager);
                                list.RemoveAll(delegate(BufferedIO io) { return io.Manager == close.Manager; });
                                bufferedWrites = new Queue<BufferedIO>(list);
                            }
                            finally
                            {
                                close.Handle.Set();
                            }
                        }

                        if (this.flushQueue.Count > 0)
                            flush = flushQueue.Dequeue();

                        if (this.bufferedWrites.Count > 0 && (engine.Settings.MaxWriteRate == 0 || writeLimiter.Chunks > 0))
                        {
                            write = this.bufferedWrites.Dequeue();
                            Interlocked.Add(ref writeLimiter.Chunks, -write.buffer.Count / ConnectionManager.ChunkLength);
                        }

                        if (this.bufferedReads.Count > 0 && (engine.Settings.MaxReadRate == 0 || readLimiter.Chunks > 0))
                        {
                            read = this.bufferedReads.Dequeue();
                            Interlocked.Add(ref readLimiter.Chunks, -read.Count / ConnectionManager.ChunkLength);
                        }

                        // If both the read queue and write queue are empty, then we unset the handle.
                        // Or if we have reached the max read/write rate and can't dequeue something, we unset the handle
                        if ((this.bufferedWrites.Count == 0 && this.bufferedReads.Count == 0) || (write == null && read == null))
                            SetHandleState(false);
                    }

                    if (flush != null)
                        writer.Flush(flush.Manager, flush.PieceIndex);

                    if (write != null)
                        PerformWrite(write);

                    if (read != null)
                        PerformRead(read);

                    // Wait ~100 ms before trying to read/write something again to give the rate limiting a chance to recover
                    this.threadWait.WaitOne(100, false);
                }
            }
            catch (Exception ex)
            {
                engine.RaiseCriticalException(new CriticalExceptionEventArgs(ex, engine));
            }
        }


        /// <summary>
        /// Sets the wait handle to Signaled (true) or Non-Signaled(false)
        /// </summary>
        /// <param name="set"></param>
        private void SetHandleState(bool set)
        {
            if (set)
                this.threadWait.Set();
            else
                this.threadWait.Reset();
        }

        internal void TickMonitors()
        {
            readMonitor.Tick();
            writeMonitor.Tick();
        }

        #endregion

        internal void Flush(TorrentManager manager)
        {
            writer.Flush(manager);
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            lock (queueLock)
                this.flushQueue.Enqueue(new PieceFlush(manager, index));
        }
    }
}
