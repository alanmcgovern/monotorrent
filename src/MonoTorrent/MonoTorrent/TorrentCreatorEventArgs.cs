//
// TorrentCreatorEventArgs.cs
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

namespace MonoTorrent
{
    public class TorrentCreatorEventArgs : EventArgs
    {
        #region Properties

        /// <summary>
        /// The number of bytes hashed from the current file
        /// </summary>
        public long FileBytesHashed { get; }

        /// <summary>
        /// The size of the current file
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        /// The percentage of the current file which has been hashed (range 0-1)
        /// </summary>
        public double FileCompletion
            => FileBytesHashed / (double) FileSize;

        /// <summary>
        /// The number of bytes hashed so far
        /// </summary>
        public long OverallBytesHashed { get; }

        /// <summary>
        /// The total number of bytes to hash
        /// </summary>
        public long OverallSize { get; }

        /// <summary>
        /// The percentage of the data which has been hashed (range 0-1)
        /// </summary>
        public double OverallCompletion
            => OverallBytesHashed / (double) OverallSize;

        /// <summary>
        /// The path of the current file
        /// </summary>
        public string CurrentFile { get; }

        #endregion Properties

        #region Constructors

        internal TorrentCreatorEventArgs (string file, long fileHashed, long fileTotal, long overallHashed, long overallTotal)
        {
            CurrentFile = file;
            FileBytesHashed = fileHashed;
            FileSize = fileTotal;
            OverallBytesHashed = overallHashed;
            OverallSize = overallTotal;
        }

        #endregion Constructors
    }
}
