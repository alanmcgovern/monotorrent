using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class StoppingMode : Mode
	{
		WaitHandleGroup handle = new WaitHandleGroup();
        Task stopDiskManagerTask;

		public override TorrentState State
		{
			get { return TorrentState.Stopping; }
		}

		public StoppingMode(TorrentManager manager)
			: base(manager)
		{
			CanAcceptConnections = false;
			ClientEngine engine = manager.Engine;

			if (manager.TrackerManager.CurrentTracker != null && manager.TrackerManager.CurrentTracker.Status == TrackerState.Ok)
				handle.AddHandle(manager.TrackerManager.Announce(TorrentEvent.Stopped), "Announcing");

			foreach (PeerId id in manager.Peers.ConnectedPeers)
				if (id.Connection != null)
					id.Connection.Dispose();

			manager.Peers.ClearAll();

            stopDiskManagerTask = engine.DiskManager.CloseFilesAsync (manager);

			manager.Monitor.Reset();
			manager.PieceManager.Reset();
			engine.ConnectionManager.CancelPendingConnects (manager);
			engine.Stop();
		}

		public override void HandlePeerConnected(PeerId id, Direction direction)
		{
			Manager.Engine.ConnectionManager.CleanupSocket (id);
		}

		public override void Tick(int counter)
		{
			if (handle.WaitOne(0, true) && stopDiskManagerTask.IsCompleted)
			{
				handle.Close();
				Manager.Mode = new StoppedMode(Manager);
			}
		}
	}
}
