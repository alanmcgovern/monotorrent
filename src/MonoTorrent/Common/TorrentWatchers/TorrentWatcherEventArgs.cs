//
// TorrentWatcher.cs
//
// Authors:
//   Stephane Zanoni   stephane@ethernal.net
//
// Copyright (C) 2006 Stephane Zanoni
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

namespace MonoTorrent.Common
{
    public delegate void TorrentWatcherEventHandler(object o, TorrentWatcherEventArgs e);
    /// <summary>
    /// Provides the data needed to handle a TorrentWatcher event
    /// </summary>
    public class TorrentWatcherEventArgs : EventArgs
    {
        #region Member Variables
        /// <summary>
        /// The path of the torrent
        /// </summary>
        public string TorrentPath
        {
            get { return this.torrentPath; }
        }
        private string torrentPath;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TorrentWatcherEventArgs
        /// </summary>
        /// <param name="torrent">The torrent which is affected</param>
        public TorrentWatcherEventArgs(string torrentPath)
        {
            this.torrentPath = torrentPath;
        }
        #endregion
    }
}
