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
    public class TorrentWatchers : List<ITorrentWatcher>
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

        private List<ITorrentWatcher> watcherList;

        public delegate void TorrentFound(string torrentPath);
        public delegate void TorrentLost(string torrentPath);

        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        public TorrentWatchers()
        {
            this.watcherList = new List<ITorrentWatcher>();
        }
        #endregion


        #region Methods
        /// <summary>
        /// Returns the ITorrentWatcher at the specified index
        /// </summary>
        /// <param name="index">The index of the ITorrentWatcher to return</param>
        /// <returns></returns>
        public ITorrentWatcher this[int index]
        {
            get { return this.watcherList[index]; }
            set { this.watcherList[index] = value; }
        }


        /// <summary>
        /// Adds a ITorrentWatcher to the TorrentWatchers
        /// </summary>
        /// <param name="torrentWatcher">The ITorrentWatcher to add</param>
        public void Add(ITorrentWatcher torrentWatcher)
        {
            torrentWatcher.Register(RaiseTorrentFound, RaiseTorrentLost);

            this.watcherList.Add(torrentWatcher);
        }


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
        /// Removes the supplied ITorrentWatcher from the list
        /// </summary>
        /// <param name="torrentWatcher">The ITorrentWatcher to remove</param>
        /// <returns>True if the ITorrentWatcher was removed</returns>
        public bool Remove(ITorrentWatcher torrentWatcher)
        {
            torrentWatcher.StopWatching();
            return this.watcherList.Remove(torrentWatcher);
        }


        /// <summary>
        /// Returns the number of ITorrentWatcher in the list
        /// </summary>
        public int Count
        {
            get { return this.watcherList.Count; }
        }


        /// <summary>
        /// Checks if the specified ITorrentWatcher is in the list
        /// </summary>
        /// <param name="torrentWatcher">The ITorrentWatcher to check</param>
        /// <returns>True if the ITorrentWatcher was found, false otherwise</returns>
        public bool Contains(ITorrentWatcher torrentWatcher)
        {
            return (this.watcherList.Contains(torrentWatcher));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<ITorrentWatcher>.Enumerator GetEnumerator()
        {
            return this.watcherList.GetEnumerator();
        }


        /// <summary>
        /// 
        /// </summary>
        public void StartWatching()
        {
            foreach (ITorrentWatcher torrentWatcher in this.watcherList)
            {
                torrentWatcher.StartWatching();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void StopWatching()
        {
            foreach (ITorrentWatcher torrentWatcher in this.watcherList)
            {
                torrentWatcher.StopWatching();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void ForceScan()
        {
            foreach (ITorrentWatcher torrentWatcher in this.watcherList)
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
    }
}

