using System;
using System.IO;

namespace MonoTorrent.TorrentWatcher
{
    public class TorrentFolderWatcher : ITorrentWatcher
    {
        #region Events

        public event EventHandler<TorrentWatcherEventArgs> TorrentFound;
        public event EventHandler<TorrentWatcherEventArgs> TorrentLost;

        #endregion Events

        #region Member Variables

        private FileSystemWatcher watcher;
        private readonly string torrentDirectory;
        private readonly string watchFilter;

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

        public void ForceScan()
        {
            foreach (var path in Directory.GetFiles(torrentDirectory, watchFilter))
                RaiseTorrentFound(path);
        }

        public void Start()
        {
            if (watcher == null)
            {
                watcher = new FileSystemWatcher(torrentDirectory);
                watcher.Filter = watchFilter;
                //this.watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Created += OnCreated;
                watcher.Deleted += OnDeleted;
            }
            watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
        }

        #endregion

        #region Event Handlers

        /// <summary>Gets called when a File with .torrent extension was added to the torrentDirectory</summary>
        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            RaiseTorrentFound(e.FullPath);
        }

        /// <summary>Gets called when a File with .torrent extension was deleted from the torrentDirectory</summary>
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            RaiseTorrentLost(e.FullPath);
        }

        protected virtual void RaiseTorrentFound(string path)
        {
            if (TorrentFound != null)
                TorrentFound(this, new TorrentWatcherEventArgs(path));
        }

        protected virtual void RaiseTorrentLost(string path)
        {
            if (TorrentLost != null)
                TorrentLost(this, new TorrentWatcherEventArgs(path));
        }

        #endregion
    }
}