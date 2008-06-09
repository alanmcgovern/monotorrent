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
using System.Threading;
using System.Xml.Serialization;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    /// <summary>
    /// This class manages writing and reading of pieces from the disk
    /// </summary>
    public class FileManager
    {
        #region Public Events

        public event EventHandler<BlockEventArgs> BlockWritten;

        #endregion Public Events


        #region Private Members

        private string baseDirectory;                           // The base directory into which all the files will be put
        private long fileSize;                                  // The combined length of all the files
        private SHA1 hasher;                                    // The SHA1 hasher used to calculate the hash of a piece
        private string savePath;                                // The path where the base directory will be put
        private TorrentManager manager;
        private TorrentFile[] files;
        private int pieceLength;

        #endregion


        #region Properties

        public string BaseDirectory
        {
            get { return baseDirectory; }
        }

        public TorrentFile[] Files
        {
            get { return files; }
        }

        public long FileSize
        {
            get { return fileSize; }
        }

        /// <summary>
        /// The length of a piece in bytes
        /// </summary>
        internal int PieceLength
        {
            get { return this.pieceLength; }
        }

        ///// <summary>
        ///// Returns the number of pieces which are currently queued in the write buffer
        ///// </summary>
        //internal int QueuedWrites
        //{
        //    get { return this.bufferedWrites.Count; }
        //}

        /// <summary>
        /// 
        /// </summary>
        internal string SavePath
        {
            get { return this.savePath; }
        }

        /// <summary>
        /// Returns true if the write streams are open.
        /// </summary>
        public bool StreamsOpen
        {
            get { return true; }// return this.fileStreams != null; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new FileManager with the supplied FileAccess
        /// </summary>
        /// <param name="files">The TorrentFiles you want to create/open on the disk</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the baseDirectory</param>
        /// <param name="pieceLength">The length of a "piece" for this file</param>
        /// <param name="fileAccess">The access level for the files</param>
        internal FileManager(TorrentManager manager, TorrentFile[] files, int pieceLength, string savePath, string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
            this.hasher = new SHA1Fast();
            this.manager = manager;
            this.savePath = savePath;
            this.files = files;
            this.pieceLength = pieceLength;

            foreach (TorrentFile file in files)
                fileSize += file.Length;
        }

        internal bool CheckFilesExist()
        {
            return Array.Exists<TorrentFile>(files, delegate(TorrentFile f) {
                return File.Exists(GenerateFilePath(f, BaseDirectory, savePath));
            });
        }

        #endregion


        #region Methods

        /// <summary>
        /// Generates the full path to the supplied TorrentFile
        /// </summary>
        /// <param name="file">The TorrentFile to generate the full path to</param>
        /// <param name="baseDirectory">The name of the directory that the files are contained in</param>
        /// <param name="savePath">The path to the directory that contains the BaseDirectory</param>
        /// <returns>The full path to the TorrentFile</returns>
        private static string GenerateFilePath(TorrentFile file, string baseDirectory, string savePath)
        {
            string path = string.Empty;

            path = Path.Combine(savePath, baseDirectory);
            path = Path.Combine(path, file.Path);

            if (!Directory.Exists(Path.GetDirectoryName(path)) && !string.IsNullOrEmpty(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            return path;
        }


        /// <summary>
        /// Generates the hash for the given piece
        /// </summary>
        /// <param name="pieceIndex">The piece to generate the hash for</param>
        /// <returns>The 20 byte SHA1 hash of the supplied piece</returns>
        internal byte[] GetHash(int pieceIndex, bool asynchronous)
        {
            int bytesToRead = 0;
            long pieceStartIndex = (long)this.pieceLength * pieceIndex;
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

                io = new BufferedIO(hashBuffer, i, bytesToRead, manager);
                io.WaitHandle = new ManualResetEvent(false);
                list.Add(io);
                manager.Engine.DiskManager.QueueRead(io);

                if (bytesToRead != Piece.BlockSize)
                    break;
            }

            lock (hasher)
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
            }

            hasher.TransformFinalBlock(hashBuffer.Array, hashBuffer.Offset, 0);
            return hasher.Hash;
        }


        /// <summary>
        /// Loads fast resume data if it exists
        /// </summary>
        /// <param name="manager">The manager to load fastresume data for</param>
        /// <returns></returns>
        /*internal static bool LoadFastResume(TorrentManager manager)
        {
            try
            {
                // FIXME: #warning Store all the fast resume in a 'data' file in a known location instead?
                // If we don't know where the .torrent is on disk, then don't save
                // fast resume data.
                if (!manager.Settings.FastResumeEnabled || string.IsNullOrEmpty(manager.Torrent.TorrentPath))
                    return false;

                string fastResumePath = manager.Torrent.TorrentPath + ".fresume";
                // We can't load fast resume data if we don't have a filepath
                if (!manager.Settings.FastResumeEnabled || !File.Exists(fastResumePath))
                    return false;

                XmlSerializer fastResume = new XmlSerializer(typeof(int[]));
                using (FileStream file = File.OpenRead(fastResumePath))
                    manager.PieceManager.MyBitField.FromArray((int[])fastResume.Deserialize(file), manager.Torrent.Pieces.Count);

                // We need to delete the old fast resume data so in the event of a crash we don't 
                // accidently reload it and think we've downloaded less data than we actually have
                File.Delete(fastResumePath);
                return true;
            }
            catch
            {
                manager.PieceManager.MyBitField.SetAll(false);
                return false;
            }
        }*/


        /// <summary>
        /// Moves all files from the current path to the new path. The existing directory structure is maintained
        /// </summary>
        /// <param name="path"></param>
        public void MoveFiles(string path, bool overWriteExisting)
        {
            if (manager.State != TorrentState.Stopped)
                throw new TorrentException("Cannot move the files when the torrent is active");

            manager.Engine.DiskManager.CloseFileStreams(this.manager).WaitOne();

            for (int i = 0; i < this.files.Length; i++)
            {
                string oldPath = GenerateFilePath(files[i], this.baseDirectory, this.savePath);
                string newPath = GenerateFilePath(files[i], this.baseDirectory, path);

                if (!File.Exists(oldPath))
                    continue;

                bool fileExists = File.Exists(newPath);
                if (!overWriteExisting && fileExists)
                    throw new TorrentException("File already exists and overwriting is disabled");

                if (fileExists)
                    File.Delete(newPath);

                File.Move(oldPath, newPath);
            }

            this.savePath = path;
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
            manager.Engine.DiskManager.QueueWrite(data);
        }


        internal void RaiseBlockWritten(BlockEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<BlockEventArgs>(BlockWritten, this.manager, args);
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
            return manager.Engine.DiskManager.Read(this.manager, buffer, bufferOffset, offset, count);
        }

        #endregion
    }
}
