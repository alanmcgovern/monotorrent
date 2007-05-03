//
// TorrentFile.cs
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



using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    /// <summary>
    /// This is the base class for the files available to download from within a .torrent.
    /// This should be inherited by both Client and Tracker "TorrentFile" classes
    /// </summary>
    public class TorrentFile
    {
        #region Member Variables
        /// <summary>
        /// The priority of this torrent file
        /// </summary>
        public Priority Priority
        {
            get { return this.priority; }
            set { this.priority = value; }
        }
        private Priority priority;


        /// <summary>
        /// The length of the file in bytes
        /// </summary>
        public long Length
        {
            get { return length; }
        }
        private long length;


        /// <summary>
        /// The index of the first piece of this file
        /// </summary>
        public int StartPieceIndex
        {
            get { return this.startPiece; }
            internal set { this.startPiece = value; }
        }
        private int startPiece;


        /// <summary>
        /// The index of the last piece of this file
        /// </summary>
        public int EndPieceIndex
        {
            get { return this.endPiece; }
            internal set { this.endPiece = value; }
        }
        private int endPiece;


        /// <summary>
        /// In the case of a single torrent file, this is the name of the file.
        /// In the case of a multi-file torrent this is the relative path of the file
        /// (including the filename) from the base directory
        /// </summary>
        public string Path
        {
            get { return path; }
        }
        private string path;


        /// <summary>
        /// The MD5 hash of the file
        /// </summary>
        public byte[] MD5
        {
            get { return this.md5; }
        }
        private byte[] md5;


        /// <summary>
        /// The ED2K hash of the file
        /// </summary>
        public byte[] ED2K
        {
            get { return ed2k; }
        }
        private byte[] ed2k;


        /// <summary>
        /// The SHA1 hash of the file
        /// </summary>
        public byte[] SHA1
        {
            get { return this.sha1; }
        }
        private byte[] sha1;
        #endregion


        #region Constructors
        public TorrentFile(string path, long length)
            : this(path, length, Priority.Normal, null, null, null)
        {
        }

        public TorrentFile(string path, long length, Priority priority)
            : this(path, length, priority, null, null, null)
        {
        }

        /// <summary>
        /// Class representing a file that is available for download from a .torrent file
        /// </summary>
        /// <param name="length">The length of the file in bytes</param>
        /// <param name="path">The filename (and path) of the file</param>
        /// <param name="md5">The MD5 hash of the file</param>
        /// <param name="ed2k">The ED2K hash of the file</param>
        /// <param name="sha1">The SHA1 hash of the file</param>
        public TorrentFile(string path, long length, Priority priority, byte[] md5, byte[] ed2k, byte[] sha1)
        {
            this.path = path;
            this.length = length;
            this.priority = priority;
            this.md5 = md5;
            this.ed2k = ed2k;
            this.sha1 = sha1;
        }
        #endregion
    }
}