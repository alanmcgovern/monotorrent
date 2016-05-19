using System;

namespace MonoTorrent.Client
{
    public class PeerExchangePeersAdded : PeersAddedEventArgs
    {
        public PeerExchangePeersAdded(TorrentManager manager, int count, int total, PeerId id)
            : base(manager, count, total)
        {
            if (id == null)
                throw new ArgumentNullException("id");

            Id = id;
        }

        public PeerId Id { get; }
    }
}