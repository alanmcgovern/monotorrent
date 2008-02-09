using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriter;

namespace MonoTorrent.Client.Managers
{
    public class DiskManager : IDisposable
    {
        #region Member Variables

        Queue<BufferedFileRead> bufferedReads;
        Queue<BufferedIO> bufferedWrites;
        private ClientEngine engine;

        private ConnectionMonitor monitor;
        internal RateLimiter rateLimiter;
        private IPieceWriter writer;

        #endregion Member Variables


        #region Old Variables

        private bool ioActive;                                  // Used to signal when the IO thread is running
        private Thread ioThread;                                // The dedicated thread used for reading/writing
        private object queueLock;                               // Used to synchronise access on the IO thread
        internal ReaderWriterLock streamsLock;
        private ManualResetEvent threadWait;                    // Used to signal the IO thread when some data is ready for it to work on

        #endregion Old Variables


        #region Properties

        internal ConnectionMonitor Monitor
        {
            get { return monitor; }
        }

        public int QueuedWrites
        {
            get { return this.bufferedWrites.Count; }
        }

        public double ReadRate
        {
            get { return monitor.UploadSpeed; }
        }

        public double WriteRate
        {
            get { return monitor.DownloadSpeed; }
        }

        public long TotalRead
        {
            get { return monitor.DataBytesUploaded; }
        }

        public long TotalWritten
        {
            get { return monitor.DataBytesDownloaded; }
        }

        #endregion Properties


        #region Constructors

        internal DiskManager(ClientEngine engine, IPieceWriter writer)
        {
            this.bufferedReads = new Queue<BufferedFileRead>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.engine = engine;
            this.ioActive = true;
            this.ioThread = new Thread(new ThreadStart(RunIO));
            this.monitor = new ConnectionMonitor();
            this.queueLock = new object();
            this.rateLimiter = new RateLimiter();
            this.streamsLock = new ReaderWriterLock();
            this.threadWait = new ManualResetEvent(false);
            this.writer = writer;
            this.ioThread.Start();
        }

        #endregion Constructors


        #region Methods

        internal void CloseFileStreams(TorrentManager manager)
        {
            writer.CloseFileStreams(manager);
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
        private void PerformWrite(BufferedIO bufferedFileIO)
        {
            PeerIdInternal id = bufferedFileIO.Id;
            ArraySegment<byte> recieveBuffer = bufferedFileIO.Buffer;
            PieceMessage message = (PieceMessage)bufferedFileIO.Message;
            Piece piece = bufferedFileIO.Piece;

            // Find the block that this data belongs to and set it's state to "Written"
            int index = PiecePickerBase.GetBlockIndex(piece.Blocks, message.StartOffset, message.RequestLength);

            // Perform the actual write
            using (new ReaderLock(this.streamsLock))
            {
                // Calculate the index where we will start to write the data
                long writeIndex = (long)message.PieceIndex * message.PieceLength + message.StartOffset;
                writer.Write(bufferedFileIO, recieveBuffer.Array, recieveBuffer.Offset, writeIndex, message.RequestLength);
            }

            piece.Blocks[index].Written = true;
            id.TorrentManager.FileManager.RaiseBlockWritten(new BlockEventArgs(id.TorrentManager, piece.Blocks[index], piece, id));

            // Release the buffer back into the buffer manager.
            //ClientEngine.BufferManager.FreeBuffer(ref bufferedFileIO.Buffer);
#warning FIX THIS - don't free the buffer here anymore

            // If we haven't written all the pieces to disk, there's no point in hash checking
            if (!piece.AllBlocksWritten)
                return;

            // Hashcheck the piece as we now have all the blocks.
            bool result = id.TorrentManager.Torrent.Pieces.IsValid(id.TorrentManager.FileManager.GetHash(piece.Index, false), piece.Index);
            id.TorrentManager.Bitfield[message.PieceIndex] = result;
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
        private void PerformRead(BufferedFileRead io)
        {
            io.BytesRead = writer.Read(io.Manager, io.Buffer, io.BufferOffset, io.PieceStartIndex, io.Count);
            io.WaitHandle.Set();
        }


        internal int Read(FileManager fileManager, byte[] buffer, int bufferOffset, long pieceStartIndex, int bytesToRead)
        {
            return writer.Read(fileManager, buffer, bufferOffset, pieceStartIndex, bytesToRead);
        }


        /// <summary>
        /// Queues a block of data to be written asynchronously
        /// </summary>
        /// <param name="id">The peer who sent the block</param>
        /// <param name="recieveBuffer">The array containing the block</param>
        /// <param name="message">The PieceMessage</param>
        /// <param name="piece">The piece that the block to be written is part of</param>
        internal void QueueWrite(PeerIdInternal id, ArraySegment<byte> recieveBuffer, PieceMessage message, Piece piece)
        {
            lock (this.queueLock)
            {
                // Request a new buffer from the buffermanager and copy the data from the receive buffer
                // into this new buffer. This is needed as the main code will automatically release the receive buffer
                // and we will lose the data.
                ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
                ClientEngine.BufferManager.GetBuffer(ref buffer, recieveBuffer.Count);
                Buffer.BlockCopy(recieveBuffer.Array, recieveBuffer.Offset, buffer.Array, buffer.Offset, recieveBuffer.Count);

                bufferedWrites.Enqueue(new BufferedIO(id, buffer, message, piece));
                SetHandleState(true);
            }
        }


        internal void QueueRead(BufferedFileRead io)
        {
            lock (this.queueLock)
            {
                bufferedReads.Enqueue(io);
                SetHandleState(true);
            }
        }


        /// <summary>
        /// This method runs in a dedicated thread. It performs all the async reads and writes as they are queued
        /// </summary>
        private void RunIO()
        {
            BufferedIO write;
            BufferedFileRead read;
            while (ioActive)
            {
                write = null;
                read = null;

                // Take a lock on the queue and dequeue any reads/writes that are available. Then lose the lock before
                // performing the actual read/write to avoid blocking other threads
                lock (this.queueLock)
                {
                    if (this.bufferedWrites.Count > 0 && (engine.Settings.MaxWriteRate == 0 || rateLimiter.DownloadChunks > 0))
                    {
                        write = this.bufferedWrites.Dequeue();
                        Interlocked.Add(ref rateLimiter.DownloadChunks, -write.Buffer.Count / ConnectionManager.ChunkLength);
                    }

                    if (this.bufferedReads.Count > 0 && (engine.Settings.MaxReadRate == 0 || rateLimiter.UploadChunks > 0))
                    {
                        read = this.bufferedReads.Dequeue();
                        Interlocked.Add(ref rateLimiter.UploadChunks, -read.Count / ConnectionManager.ChunkLength);
                    }

                    // If both the read queue and write queue are empty, then we unset the handle.
                    // Or if we have reached the max read/write rate and can't dequeue something, we unset the handle
                    if ((this.bufferedWrites.Count == 0 && this.bufferedReads.Count == 0) || (write == null && read == null))
                        SetHandleState(false);
                }

                if (write != null)
                    PerformWrite(write);

                if (read != null)
                    PerformRead(read);

                // Wait ~100 ms before trying to read/write something again to give the rate limiting a chance to recover
                this.threadWait.WaitOne(100, false);
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

        #endregion

        internal void Flush(TorrentManager manager)
        {
            writer.Flush(manager);
        }
    }
}
