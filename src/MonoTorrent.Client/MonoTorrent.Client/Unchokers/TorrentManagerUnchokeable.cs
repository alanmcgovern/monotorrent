using System;
using System.Collections.Generic;

namespace MonoTorrent.Client
{
    class TorrentManagerUnchokeable : IUnchokeable
    {
        TorrentManager Manager { get; }

        public event EventHandler<TorrentStateChangedEventArgs> StateChanged {
            add { Manager.TorrentStateChanged += value; }
            remove { Manager.TorrentStateChanged -= value; }
        }

        public bool Seeding => Manager.Complete;

        public long DownloadSpeed => Manager.Monitor.DownloadSpeed;

        public long UploadSpeed => Manager.Monitor.UploadSpeed;

        public long MaximumDownloadSpeed => Manager.Settings.MaximumDownloadSpeed;

        public long MaximumUploadSpeed => Manager.Settings.MaximumUploadSpeed;

        public int UploadSlots => Manager.Settings.UploadSlots;

        public int UploadingTo {
            get => Manager.UploadingTo;
            set => Manager.UploadingTo = value;
        }

        public List<PeerId> Peers => Manager.Peers.ConnectedPeers;

        public TorrentManagerUnchokeable (TorrentManager manager)
        {
            Manager = manager;
        }
    }
}
