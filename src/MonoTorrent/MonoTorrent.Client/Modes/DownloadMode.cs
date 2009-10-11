using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    class DownloadMode : Mode
    {
        TorrentState state;
		public override TorrentState State
		{
			get { return state; }
		}

        public DownloadMode(TorrentManager manager)
            : base(manager)
        {
            state = manager.Complete ? TorrentState.Seeding : TorrentState.Downloading;
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
            //If download is complete, set state to 'Seeding'
            if (Manager.Complete && state == TorrentState.Downloading)
            {
                state = TorrentState.Seeding;
                Manager.RaiseTorrentStateChanged(new TorrentStateChangedEventArgs(Manager, TorrentState.Downloading, TorrentState.Seeding));
                Manager.TrackerManager.Announce(TorrentEvent.Completed);
            }
            for (int i = 0; i < Manager.Peers.ConnectedPeers.Count; i++)
                if (!ShouldConnect(Manager.Peers.ConnectedPeers[i]))
                    Manager.Peers.ConnectedPeers[i].CloseConnection();
            base.Tick(counter);
        }
    }
}
