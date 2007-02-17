//
// FileManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//



using MonoTorrent.Common;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using MonoTorrent.Client.PeerMessages;
using System.Threading;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class manages writing and reading of pieces from the disk
    /// </summary>
    internal class FileManager : System.IDisposable
    {
        #region Private Members
        
        TorrentFile[] files; 
        string baseDirectory; 
        string savePath;
        private readonly int pieceLength;
        private long fileSize;
        private SHA1Managed hasher;
        private FileStream[] fileStreams;
        private bool initialHashRequired;
        private Thread ioThread;
        private bool ioActive;
        private ManualResetEvent threadWait;
        
        #endregion


        #region Properties

        /// <summary>
        /// True if we need to hash the files (i.e. some were preexisting)
        /// </summary>
        internal bool InitialHashRequired
        {
            get { return this.initialHashRequired; }
            set { this.initialHashRequired = value; }
        }

        /// <summary>
        /// Returns the number of pieces which are currently queued in the write buffer
        /// </summary>
        internal int QueuedWrites
        {
            get { return this.bufferedWrites.Count; }
        }

        /// <summary>
        /// The length of a piece in bytes
        /// </summary>
        public int PieceLength
        {
            get { return this.pieceLength; }
        }

        /// <summary>
        /// Returns true if the write streams are open.
        /// </summary>
        public bool StreamsOpen
        {
            get { return this.fileStreams != null; }
        }

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new FileManager with read-only access
        /// </summary>
        /// <param name="file">The TorrentFile to open/create on disk</param>
        /// <param name="savePath">The directory the file should be contained in</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        public FileManager(TorrentFile file, string savePath, int pieceLength)
            : this(new TorrentFile[] { file }, string.Empty, savePath, pieceLength, FileAccess.Read)
        {
        }

        /// <summary>
        /// Creates a new FileManager with the supplied FileAccess
        /// </summary>
        /// <param name="file">The TorrentFile you want to create/open on the disk</param>
        /// <param name="savePath">The path to the directory that the file should be contained in</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        /// <param name="fileAccess">The access level for the file</param>
        public FileManager(TorrentFile file, string savePath, int pieceLength, FileAccess fileAccess)
            : this(new TorrentFile[] { file }, string.Empty, savePath, pieceLength, fileAccess)
        {
        }

        /// <summary>
        /// Creates a new FileManager with read-only access
        /// </summary>
        /// <param name="files">The TorrentFiles you want to create/open on the disk</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the baseDirectory</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        public FileManager(TorrentFile[] files, string baseDirectory, string savePath, int pieceLength)
            : this(files, baseDirectory, savePath, pieceLength, FileAccess.Read)
        {
        }

        /// <summary>
        /// Creates a new FileManager with the supplied FileAccess
        /// </summary>
        /// <param name="files">The TorrentFiles you want to create/open on the disk</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the baseDirectory</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        /// <param name="fileAccess">The access level for the files</param>
        public FileManager(TorrentFile[] files, string baseDirectory, string savePath, int pieceLength, FileAccess fileAccess)
        {
            if (files.Length == 1)
                this.baseDirectory = string.Empty;
            else
                this.baseDirectory = baseDirectory;

            this.files = files;
            this.savePath = savePath;
            this.initialHashRequired = false;
            this.pieceLength = pieceLength;
            this.hasher = new SHA1Managed();
            this.hashBuffer = new byte[pieceLength];

            if (files.Length == 1)
                baseDirectory = string.Empty;

            this.ioActive = true;
            this.threadWait = new ManualResetEvent(false);
        }
        #endregion


        internal void OpenFileStreams(FileAccess fileAccess)
        {
            string filePath = null;
            this.fileStreams = new FileStream[files.Length];

            for (int i = 0; i < this.fileStreams.Length; i++)
            {
                filePath = GenerateFilePath(this.files[i], this.baseDirectory, this.savePath);

                if (File.Exists(filePath))
                    this.initialHashRequired = true;

                this.fileStreams[i] = new FileStream(filePath, FileMode.OpenOrCreate, fileAccess, FileShare.Read, 65536);

                // This hashing algorithm is written on the basis that the files are
                // preallocated. Might change to not have to preallocate files in future,
                // but there's no benefits to doing that.
                if (this.fileStreams[i].Length != this.files[i].Length && fileAccess == FileAccess.ReadWrite)
                    this.fileStreams[i].SetLength(files[i].Length);

                this.fileSize += files[i].Length;
            }

            SetHandleState(true);
            this.ioActive = true;
            this.ioThread = new Thread(new ThreadStart(this.RunIO));
            this.ioThread.Start();
        }

        internal void CloseFileStreams()
        {
            for (int i = 0; i < this.fileStreams.Length; i++)
                this.fileStreams[i].Dispose();

            this.fileStreams = null;

            // Setting this boolean true allows the IO thread to terminate gracefully
            this.ioActive = false;

            // Allow the IO thread to run.
            SetHandleState(true);
        }

        #region Methods
        /// <summary>
        /// Generates the full path to the supplied TorrentFile
        /// </summary>
        /// <param name="file">The TorrentFile to generate the full path to</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the BaseDirectory</param>
        /// <returns>The full path to the TorrentFile</returns>
        private string GenerateFilePath(TorrentFile file, string baseDirectory, string savePath)
        {
            string path = string.Empty;

            path = Path.Combine(savePath, baseDirectory);
            path = Path.Combine(path, file.Path);

            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
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
        internal int Read(byte[] buffer, int bufferOffset, long offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > this.fileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int bytesRead = 0;
            int totalRead = 0;

            for (i = 0; i < this.fileStreams.Length; i++)       // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < this.fileStreams[i].Length)        // the start of the data we want to read
                    break;

                offset -= this.fileStreams[i].Length;           // Offset now contains the index of the data we want
            }                                                   // to read from fileStream[i].

            while (totalRead < count)                           // We keep reading until we have read 'count' bytes.
            {
                if (i == fileStreams.Length)
                    break;

                lock (this.fileStreams[i])
                {
                    fileStreams[i].Seek(offset, SeekOrigin.Begin);
                    offset = 0; // Any further files need to be read from the beginning
                    bytesRead = fileStreams[i].Read(buffer, bufferOffset + totalRead, count - totalRead);
                    totalRead += bytesRead;
                    i++;
                }
            }

            return totalRead;
        }


        /// <summary>
        /// This method reads 'count' number of bytes starting at the position 'offset' into the
        /// byte[] 'buffer'. The data gets written in the buffer starting at index 'bufferOffset'
        /// </summary>
        /// <param name="buffer">The byte[] to read the data into</param>
        /// <param name="bufferOffset">The offset within the array to save the data</param>
        /// <param name="offset">The offset in the file from which to read the data</param>
        /// <param name="count">The number of bytes to read</param>
        internal void Write(byte[] buffer, int bufferOffset, long offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > this.fileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            long bytesWritten = 0;
            long totalWritten = 0;
            long bytesWeCanWrite = 0;

            for (i = 0; i < this.fileStreams.Length; i++)       // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < this.fileStreams[i].Length)        // the start of the data we want to write
                    break;

                offset -= this.fileStreams[i].Length;           // Offset now contains the index of the data we want
            }                                                   // to write to fileStream[i].

            while (totalWritten < count)                        // We keep writing  until we have written 'count' bytes.
            {
                if (i == fileStreams.Length)
                    break;

                lock (this.fileStreams[i])
                {
                    fileStreams[i].Seek(offset, SeekOrigin.Begin);
                    offset = 0; // Any further files need to be written from the beginning of the file

                    bytesWeCanWrite = this.fileStreams[i].Length - this.fileStreams[i].Position;
                    bytesWritten = (bytesWeCanWrite < (count - totalWritten)) ? bytesWeCanWrite : (count - totalWritten);

                    this.fileStreams[i].Write(buffer, bufferOffset + (int)totalWritten, (int)bytesWritten);

                    totalWritten += bytesWritten;
                    i++;
                }
            }
        }


        /// <summary>
        /// Flushes all data in the FileStreams to disk
        /// </summary>
        internal void FlushAll()
        {
            foreach (FileStream stream in this.fileStreams)
                lock (stream)
                    stream.Flush();
        }


        private byte[] hashBuffer;
        /// <summary>
        /// Generates the hash for the given piece
        /// </summary>
        /// <param name="pieceIndex">The piece to generate the hash for</param>
        /// <returns>The 20 byte SHA1 hash of the supplied piece</returns>
        internal byte[] GetHash(int pieceIndex)
        {
            lock (this.hashBuffer)
            {
                long pieceStartIndex = (long)this.pieceLength * pieceIndex;
                int bytesRead = this.Read(this.hashBuffer, 0, pieceStartIndex, (int)(this.fileSize - pieceStartIndex > this.pieceLength ? this.pieceLength : this.fileSize - pieceStartIndex));
                return hasher.ComputeHash(this.hashBuffer, 0, bytesRead);
            }
        }
        

        /// <summary>
        /// Disposes all necessary objects
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }


        /// <summary>
        /// Disposes all necessary objects
        /// </summary>
        public void Dispose(bool disposing)
        {
            hasher.Clear();
            if (this.StreamsOpen)
                CloseFileStreams();
        }
        #endregion

        private object bufferedIoLock = new object();
        private Queue<BufferedFileWrite> bufferedWrites = new Queue<BufferedFileWrite>();
        private Queue<BufferedFileWrite> bufferedReads = new Queue<BufferedFileWrite>();

        internal void QueueWrite(PeerConnectionID id, byte[] recieveBuffer, PieceMessage message, Piece piece)
        {
            lock (this.bufferedIoLock)
            {
                byte[] buffer = BufferManager.EmptyBuffer;
                ClientEngine.BufferManager.GetBuffer(ref buffer, BufferType.LargeMessageBuffer);
                Buffer.BlockCopy(recieveBuffer, 0, buffer, 0, recieveBuffer.Length);

                bufferedWrites.Enqueue(new BufferedFileWrite(id, buffer, message, piece, id.TorrentManager.Bitfield));
                SetHandleState(true);
            }
        }

        internal void QueueRead(PeerConnectionID id, byte[] recieveBuffer, RequestMessage message, Piece piece)
        {
            lock (this.bufferedIoLock)
            {
                this.bufferedReads.Enqueue(new BufferedFileWrite(id, recieveBuffer, message, piece, id.TorrentManager.Bitfield));
                SetHandleState(true);
            }
        }

        private void RunIO()
        {
            BufferedFileWrite write;
            BufferedFileWrite read;
            while (ioActive)
            {
                write = null;
                read = null;
                lock (this.bufferedIoLock)
                {
                    if (this.bufferedWrites.Count > 0)
                        write = this.bufferedWrites.Dequeue();

                    if (this.bufferedReads.Count > 0)
                        read = this.bufferedReads.Dequeue();

                    if (this.bufferedReads.Count == 0 && this.bufferedWrites.Count == 0)
                        SetHandleState(false);
                }

                if (write != null)
                    PerformWrite(write);

                if (read != null)
                    PerformRead(read);

                this.threadWait.WaitOne();
            }
        }

        private void PerformWrite(BufferedFileWrite bufferedFileIO)
        {
            PeerConnectionID id = bufferedFileIO.Id;
            byte[] recieveBuffer = bufferedFileIO.Buffer;
            PieceMessage message = (PieceMessage)bufferedFileIO.Message;
            Piece piece = bufferedFileIO.Piece;

            long writeIndex = (long)message.PieceIndex * message.PieceLength + message.StartOffset;
            this.Write(recieveBuffer, message.DataOffset, writeIndex, message.BlockLength);
            for (int i = 0; i < piece.Blocks.Length; i++)
            {
                if (piece[i].StartOffset != message.StartOffset)
                    continue;

                piece[i].Written = true;
                break;
            }
            ClientEngine.BufferManager.FreeBuffer(ref bufferedFileIO.Buffer);

            if (!piece.AllBlocksWritten)
                return;

            bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[piece.Index], id.TorrentManager.FileManager.GetHash(piece.Index));
            bufferedFileIO.BitField[message.PieceIndex] = result;

            id.TorrentManager.HashedPiece(new PieceHashedEventArgs(piece.Index, result));

            if (result)
                lock (id.TorrentManager.finishedPieces)
                    id.TorrentManager.finishedPieces.Enqueue(piece.Index);
            else
                id.Peer.HashFails++;
        }

        private void PerformRead(BufferedFileWrite bufferedFileIO)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void SetHandleState(bool set)
        {
            if (set)
                this.threadWait.Set();
            else
                this.threadWait.Reset();
        }
    }
}