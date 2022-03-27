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

namespace MonoTorrent.TorrentWatcher
{
    public class TorrentFolderWatcher : ITorrentWatcher
    {
        #region Events

        public event EventHandler<TorrentWatcherEventArgs>? TorrentFound;
        public event EventHandler<TorrentWatcherEventArgs>? TorrentLost;

        #endregion Events


        #region Member Variables

        FileSystemWatcher watcher;
        readonly string torrentDirectory;
        readonly string watchFilter;

        #endregion


        #region Constructors

        public TorrentFolderWatcher (string torrentDirectory, string watchFilter)
        {
            if (torrentDirectory == null)
                throw new ArgumentNullException (nameof (torrentDirectory));

            if (watchFilter == null)
                throw new ArgumentNullException (nameof (watchFilter));

            if (!Directory.Exists (torrentDirectory))
                Directory.CreateDirectory (torrentDirectory);

            this.torrentDirectory = torrentDirectory;
            this.watchFilter = watchFilter;

            watcher = new FileSystemWatcher (torrentDirectory) {
                Filter = watchFilter
            };
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
        }

        public TorrentFolderWatcher (DirectoryInfo torrentDirectory)
            : this (torrentDirectory.FullName, "*.torrent")
        {

        }

        #endregion


        #region ITorrentWatcher implementations

        public void ForceScan ()
        {
            foreach (string path in Directory.GetFiles (torrentDirectory, watchFilter))
                RaiseTorrentFound (path);
        }

        public void Start ()
        {
            watcher.EnableRaisingEvents = true;
        }

        public void Stop ()
        {
            watcher.EnableRaisingEvents = false;
        }

        #endregion


        #region Event Handlers

        ///<summary>Gets called when a File with .torrent extension was added to the torrentDirectory</summary>
        void OnCreated (object sender, FileSystemEventArgs e)
        {
            RaiseTorrentFound (e.FullPath);
        }

        ///<summary>Gets called when a File with .torrent extension was deleted from the torrentDirectory</summary>
        void OnDeleted (object sender, FileSystemEventArgs e)
        {
            RaiseTorrentLost (e.FullPath);
        }

        protected virtual void RaiseTorrentFound (string path)
        {
            TorrentFound?.Invoke (this, new TorrentWatcherEventArgs (path));
        }

        protected virtual void RaiseTorrentLost (string path)
        {
            TorrentLost?.Invoke (this, new TorrentWatcherEventArgs (path));
        }

        #endregion
    }
}
