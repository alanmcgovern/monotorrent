using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class StoppingMode : Mode
	{
		List<Task> announcingTasks = new List<Task>();
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
				announcingTasks.Add(manager.TrackerManager.Announce(TorrentEvent.Stopped));

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
			if (announcingTasks.TrueForAll (t => t.IsCompleted) && stopDiskManagerTask.IsCompleted)
				Manager.Mode = new StoppedMode(Manager);
		}
	}
}
