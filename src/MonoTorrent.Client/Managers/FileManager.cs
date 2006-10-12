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
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class manages writing and reading of pieces from the disk
    /// </summary>
    public class FileManager : System.IDisposable
    {
        #region Member Variables
        /// <summary>
        /// The length of a piece in bytes
        /// </summary>
        public int PieceLength
        {
            get { return this.pieceLength; }
        }
        private readonly int pieceLength;

        private SHA1Managed hasher;

        private FileStream[] fileStreams;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new FileManager with read-only access
        /// </summary>
        /// <param name="file">The TorrentFile to open/create on disk</param>
        /// <param name="savePath">The directory the file should be contained in</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        public FileManager(ITorrentFile file, string savePath, int pieceLength)
            : this(new ITorrentFile[] { file }, string.Empty, savePath, pieceLength, FileAccess.Read)
        {
        }

        /// <summary>
        /// Creates a new FileManager with the supplied FileAccess
        /// </summary>
        /// <param name="file">The TorrentFile you want to create/open on the disk</param>
        /// <param name="savePath">The path to the directory that the file should be contained in</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        /// <param name="fileAccess">The access level for the file</param>
        public FileManager(ITorrentFile file, string savePath, int pieceLength, FileAccess fileAccess)
            : this(new ITorrentFile[] { file }, string.Empty, savePath, pieceLength, fileAccess)
        {
        }

        /// <summary>
        /// Creates a new FileManager with read-only access
        /// </summary>
        /// <param name="files">The TorrentFiles you want to create/open on the disk</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the baseDirectory</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        public FileManager(ITorrentFile[] files, string baseDirectory, string savePath, int pieceLength)
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
        public FileManager(ITorrentFile[] files, string baseDirectory, string savePath, int pieceLength, FileAccess fileAccess)
        {
            this.pieceLength = pieceLength;
            this.hasher = new SHA1Managed();
            this.hashBuffer = new byte[pieceLength];
            this.fileStreams = new FileStream[files.Length];

            if (files.Length == 1)
                baseDirectory = string.Empty;

            for (int i = 0; i < fileStreams.Length; i++)
            {
                fileStreams[i] = new FileStream(GenerateFilePath(files[i], baseDirectory, savePath), FileMode.OpenOrCreate, fileAccess, FileShare.Read);

                // This hashing algorithm is written on the basis that the files are
                // preallocated. Might change to not have to preallocate files in future,
                // but there's no benefits to doing that.
                if (fileStreams[i].Length != files[i].Length && fileAccess == FileAccess.ReadWrite)
                    fileStreams[i].SetLength(files[i].Length);
            }
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Generates the full path to the supplied ITorrentFile
        /// </summary>
        /// <param name="file">The ITorrentFile to generate the full path to</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the BaseDirectory</param>
        /// <returns>The full path to the TorrentFile</returns>
        private string GenerateFilePath(ITorrentFile file, string baseDirectory, string savePath)
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
        public int Read(byte[] buffer, int bufferOffset, long offset, int count)
        {
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
        public void Write(byte[] buffer, int bufferOffset, long offset, int count)
        {
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
                int bytesRead = this.Read(this.hashBuffer, 0, pieceStartIndex, this.pieceLength);
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

            foreach (FileStream stream in this.fileStreams)
                lock (stream)
                    stream.Close();
        }
        #endregion
    }
}