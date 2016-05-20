using System;

namespace MonoTorrent.TorrentWatcher
{
    public interface ITorrentWatcher
    {
        event EventHandler<TorrentWatcherEventArgs> TorrentFound;
        event EventHandler<TorrentWatcherEventArgs> TorrentLost;

        void Start();
        void Stop();
        void ForceScan();
    }
}