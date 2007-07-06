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
using System.IO;

namespace MonoTorrent.Common
{
    public class TorrentFolderWatcher : ITorrentWatcher
    {
        #region Events

        public event EventHandler<TorrentWatcherEventArgs> TorrentFound;
        public event EventHandler<TorrentWatcherEventArgs> TorrentLost;

        #endregion Events


        #region Member Variables

        private FileSystemWatcher watcher;
        private string torrentDirectory;
        private string watchFilter;

        #endregion


        #region Constructors

        public TorrentFolderWatcher(string torrentDirectory, string watchFilter)
        {
            if (torrentDirectory == null)
                throw new ArgumentNullException("torrentDirectory");

            if (watchFilter == null)
                throw new ArgumentNullException("watchFilter");

            if (!Directory.Exists(torrentDirectory))
                Directory.CreateDirectory(torrentDirectory);

            this.torrentDirectory = torrentDirectory;
            this.watchFilter = watchFilter;
        }

        public TorrentFolderWatcher(DirectoryInfo torrentDirectory)
            : this(torrentDirectory.FullName, "*.torrent")
        {
            
        }

        #endregion


        #region ITorrentWatcher implementations

        ///<summary>Start the FileSystemWatcher on torrentDirectory</summary>
        public void StartWatching()
        {
            if (this.watcher == null)
            {
                this.watcher = new FileSystemWatcher(torrentDirectory);
                this.watcher.Filter = this.watchFilter;
                this.watcher.Created += new FileSystemEventHandler(OnCreated);
                this.watcher.Deleted += new FileSystemEventHandler(OnDeleted);
            }
            this.watcher.EnableRaisingEvents = true;
        }

        ///<summary>Start the FileSystemWatcher on torrentDirectory</summary>
        public void StopWatching()
        {
            this.watcher.EnableRaisingEvents = false;
        }


        ///<summary>Start the FileSystemWatcher on torrentDirectory</summary>
        public void ForceScan()
        {
            Console.WriteLine("loading torrents from " + torrentDirectory);
            foreach (string path in Directory.GetFiles(torrentDirectory, this.watchFilter))
                RaiseTorrentFound(path);
        }
        #endregion


        #region Event Handlers

        ///<summary>Gets called when a File with .torrent extension was added to the torrentDirectory</summary>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            RaiseTorrentFound(e.FullPath);
        }

        ///<summary>Gets called when a File with .torrent extension was deleted from the torrentDirectory</summary>
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            RaiseTorrentLost(e.FullPath);
        }

        private void RaiseTorrentFound(string path)
        {
            if (TorrentFound != null)
                TorrentFound(this, new TorrentWatcherEventArgs(path));
        }

        private void RaiseTorrentLost(string path)
        {
            if (TorrentLost != null)
                TorrentLost(this, new TorrentWatcherEventArgs(path));
        }

        #endregion
    }
}
