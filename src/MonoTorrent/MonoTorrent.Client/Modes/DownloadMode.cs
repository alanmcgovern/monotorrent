using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    class DownloadMode : Mode
    {
        public DownloadMode(TorrentManager manager)
            : base(manager)
        {

        }

        public override void HandlePeerConnected(PeerId id, MonoTorrent.Common.Direction direction)
        {
            if (!ShouldConnect(id))
                id.CloseConnection();
            base.HandlePeerConnected(id, direction);
        }

        public override bool ShouldConnect(Peer peer)
        {
            return !(peer.IsSeeder && Manager.HasMetadata && Manager.Complete);
        }

        public override void Tick(int counter)
        {
            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++)
                if (!ShouldConnect(Manager.Peers.ConnectedPeers[i]))
                    Manager.Peers.ConnectedPeers[i].CloseConnection();
            base.Tick(counter);
        }
    }
}
