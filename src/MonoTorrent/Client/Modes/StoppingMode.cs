using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class StoppingMode : Mode
    {
        private WaitHandleGroup handle = new WaitHandleGroup();

        public override TorrentState State
        {
            get { return TorrentState.Stopping; }
        }

        public StoppingMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
            var engine = manager.Engine;
            if (manager.Mode is HashingMode)
                handle.AddHandle(((HashingMode) manager.Mode).hashingWaitHandle, "Hashing");

            if (manager.TrackerManager.CurrentTracker != null &&
                manager.TrackerManager.CurrentTracker.Status == TrackerState.Ok)
                handle.AddHandle(manager.TrackerManager.Announce(TorrentEvent.Stopped), "Announcing");

            foreach (var id in manager.Peers.ConnectedPeers)
                if (id.Connection != null)
                    id.Connection.Dispose();

            manager.Peers.ClearAll();

            handle.AddHandle(engine.DiskManager.CloseFileStreams(manager), "DiskManager");

            manager.Monitor.Reset();
            manager.PieceManager.Reset();
            engine.ConnectionManager.CancelPendingConnects(manager);
            engine.Stop();
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            id.CloseConnection();
        }

        public override void Tick(int counter)
        {
            if (handle.WaitOne(0, true))
            {
                handle.Close();
                Manager.Mode = new StoppedMode(Manager);
            }
        }
    }
}