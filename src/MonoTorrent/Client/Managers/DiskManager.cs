using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;

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
        private FileStreamBuffer streamsBuffer;


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

        public int OpenFiles
        {
            get { return streamsBuffer.Count; }
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

        public DiskManager(ClientEngine engine)
        {
            this.bufferedReads = new Queue<BufferedFileRead>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.engine = engine;
            this.ioActive = true;
            this.ioThread = new Thread(new ThreadStart(RunIO));
            this.monitor = new ConnectionMonitor();
            this.queueLock = new object();
            this.rateLimiter = new RateLimiter();
            this.streamsBuffer = new FileStreamBuffer(engine.Settings.MaxOpenFiles);
            this.streamsLock = new ReaderWriterLock();
            this.threadWait = new ManualResetEvent(false);
            this.ioThread.Start();
        }

        #endregion Constructors


        #region Methods

        internal void CloseFileStreams(TorrentManager manager)
        {
            foreach (TorrentFile file in manager.Torrent.Files)
                streamsBuffer.CloseStream(file);
        }


        /// <summary>
        /// Generates the full path to the supplied TorrentFile
        /// </summary>
        /// <param name="file">The TorrentFile to generate the full path to</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the BaseDirectory</param>
        /// <returns>The full path to the TorrentFile</returns>
        private static string GenerateFilePath(TorrentFile file, string baseDirectory, string savePath)
        {
            string path;

            path = Path.Combine(savePath, baseDirectory);
            path = Path.Combine(path, file.Path);

            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
        }


        /// <summary>
        /// Opens all the filestreams with the specified file access
        /// </summary>
        /// <param name="fileAccess"></param>
        internal TorrentFileStream GetStream(FileManager manager, TorrentFile file, FileAccess access)
        {
            string filePath = GenerateFilePath(file, manager.BaseDirectory, manager.SavePath);
            return streamsBuffer.GetStream(file, filePath, access);
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
                Write(bufferedFileIO, recieveBuffer.Array, recieveBuffer.Offset + message.DataOffset, writeIndex, message.RequestLength);
            }

            piece.Blocks[index].Written = true;
            id.TorrentManager.FileManager.RaiseBlockWritten(new BlockEventArgs(id.TorrentManager, piece.Blocks[index], piece, id));

            // Release the buffer back into the buffer manager.
            ClientEngine.BufferManager.FreeBuffer(ref bufferedFileIO.Buffer);

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
            io.BytesRead = Read(io.Manager, io.Buffer, io.BufferOffset, io.PieceStartIndex, io.Count);
            io.WaitHandle.Set();
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
        /// This method reads 'count' number of bytes from the filestream starting at index 'offset'.
        /// The bytes are read into the buffer starting at index 'bufferOffset'.
        /// </summary>
        /// <param name="buffer">The byte[] containing the bytes to write</param>
        /// <param name="bufferOffset">The offset in the byte[] at which to save the data</param>
        /// <param name="offset">The offset in the file at which to start reading the data</param>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The number of bytes successfully read</returns>
        internal int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int bytesRead = 0;
            int totalRead = 0;

            for (i = 0; i < manager.Files.Length; i++)       // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)              // the start of the data we want to read
                    break;

                offset -= manager.Files[i].Length;           // Offset now contains the index of the data we want
            }                                                   // to read from fileStream[i].

            while (totalRead < count)                           // We keep reading until we have read 'count' bytes.
            {
                if (i == manager.Files.Length)
                    break;

                lock (manager.Files[i])
                {
                    TorrentFileStream s = GetStream(manager, manager.Files[i], FileAccess.Read);
                    s.Seek(offset, SeekOrigin.Begin);
                    offset = 0; // Any further files need to be read from the beginning
                    bytesRead = s.Read(buffer, bufferOffset + totalRead, count - totalRead);
                    totalRead += bytesRead;
                    i++;
                }
            }
            monitor.BytesSent(totalRead, TransferType.Data);
            return totalRead;
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


        /// <summary>
        /// This method reads 'count' number of bytes starting at the position 'offset' into the
        /// byte[] 'buffer'. The data gets written in the buffer starting at index 'bufferOffset'
        /// </summary>
        /// <param name="buffer">The byte[] to read the data into</param>
        /// <param name="bufferOffset">The offset within the array to save the data</param>
        /// <param name="offset">The offset in the file from which to read the data</param>
        /// <param name="count">The number of bytes to read</param>
        private void Write(BufferedIO io, byte[] buffer, int bufferOffset, long offset, int count)
        {
            FileManager manager = io.Id.TorrentManager.FileManager;
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            long bytesWritten = 0;
            long totalWritten = 0;
            long bytesWeCanWrite = 0;

            for (i = 0; i < manager.Files.Length; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)           // the start of the data we want to write
                    break;

                offset -= manager.Files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to write to fileStream[i].

            while (totalWritten < count)                        // We keep writing  until we have written 'count' bytes.
            {
                lock (manager.Files[i])
                {
                    TorrentFileStream stream = GetStream(manager, manager.Files[i], FileAccess.ReadWrite);
                    stream.Seek(offset, SeekOrigin.Begin);

                    // Find the maximum number of bytes we can write before we reach the end of the file
                    bytesWeCanWrite = manager.Files[i].Length - offset;

                    // Any further files need to be written from the beginning of the file
                    offset = 0;

                    // If the amount of data we are going to write is larger than the amount we can write, just write the allowed
                    // amount and let the rest of the data be written with the next filestream
                    bytesWritten = ((count - totalWritten) > bytesWeCanWrite) ? bytesWeCanWrite : (count - totalWritten);

                    // Write the data
                    stream.Write(buffer, bufferOffset + (int)totalWritten, (int)bytesWritten);

                    // Any further data should be written to the next available file
                    totalWritten += bytesWritten;
                    i++;
                }
            }

            monitor.BytesReceived((int)totalWritten, TransferType.Data);
        }

        #endregion

        public void Dispose()
        {
            ioActive = false;
            this.threadWait.Set();
            this.ioThread.Join();
            this.streamsBuffer.Dispose();
        }
    }
}
