using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class StoppedMode : Mode
    {
        public override bool CanHashCheck
        {
            get { return true; }
        }

        public override TorrentState State
        {
            get { return TorrentState.Stopped; }
        }

        public StoppedMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            id.CloseConnection();
        }


        public override void Tick(int counter)
        {
            // When stopped, do nothing
        }
    }
}