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
using System.Collections.Generic;

namespace MonoTorrent.Common
{
    /// <summary>
    /// Main controller class for ITorrentWatcher
    /// </summary>
    public class TorrentWatchers : ITorrentWatcherCollection
    {
        #region Events
        /// <summary>
        /// Event that's fired every time a new Torrent is detected
        /// </summary>
        public event EventHandler<TorrentWatcherEventArgs> OnTorrentFound;


        /// <summary>
        /// Event that's fired every time a Torrent is removed
        /// </summary>
        public event EventHandler<TorrentWatcherEventArgs> OnTorrentLost;
        #endregion


        #region Member Variables

        public delegate void TorrentFound(string torrentPath);
        public delegate void TorrentLost(string torrentPath);

        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        public TorrentWatchers() : base()
        {
            
        }
        #endregion


        #region Methods


        /// <summary>
        /// Removes the ITorrentWatcher at the specified index
        /// </summary>
        /// <param name="index">The index to remove the ITorrentWatcher at</param>
        public void Remove(int index)
        {
            ITorrentWatcher torrentWatcher = this[index];
            this.Remove(torrentWatcher);
        }


        /// <summary>
        /// 
        /// </summary>
        public void StartWatching()
        {
            foreach (ITorrentWatcher torrentWatcher in this)
            {
                torrentWatcher.StartWatching();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void StopWatching()
        {
            foreach (ITorrentWatcher torrentWatcher in this)
            {
                torrentWatcher.StopWatching();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void ForceScan()
        {
            foreach (ITorrentWatcher torrentWatcher in this)
            {
                torrentWatcher.ForceScan();
            }
        }
        #endregion


        #region Event Throwers

        private void RaiseTorrentFound(string torrentPath)
        {
            TorrentWatcherEventArgs eventArgs = new TorrentWatcherEventArgs(torrentPath);

            if (this.OnTorrentFound != null)
                this.OnTorrentFound(this, eventArgs);
        }

        private void RaiseTorrentLost(string torrentPath)
        {
            TorrentWatcherEventArgs eventArgs = new TorrentWatcherEventArgs(torrentPath);

            if (this.OnTorrentLost != null)
                this.OnTorrentLost(this, eventArgs);
        }

        #endregion

        public bool IsReadOnly
        {
            get { return false; }
        }

    }
}

