using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	class StoppingMode : Mode
	{
		ManagerWaitHandle handle = new ManagerWaitHandle("Global");

		public override bool CanAcceptConnections
		{
			get { return false; }
		}

		public StoppingMode(TorrentManager manager)
			: base(manager)
		{
			ClientEngine engine = manager.Engine;

			// If we're in the hashing state, it means the torrent manager has not contacted the server.
			if (manager.State == TorrentState.Hashing)
			{
				handle.AddHandle(manager.HashingHandle, "Hashing");
				return;
			}

			if (manager.TrackerManager.CurrentTracker != null)
				handle.AddHandle(manager.TrackerManager.Announce(TorrentEvent.Stopped), "Announcing");

			foreach (PeerId id in manager.Peers.ConnectedPeers)
				if (id.Connection != null)
					id.Connection.Dispose();

			manager.Peers.ClearAll();

			handle.AddHandle(engine.DiskManager.CloseFileStreams(manager, manager.SavePath, manager.Torrent.Files), "DiskManager");

			manager.Monitor.Reset();
			manager.PieceManager.Reset();
			engine.ConnectionManager.CancelPendingConnects(manager);
			if (engine.ConnectionManager.IsRegistered(manager))
				engine.ConnectionManager.UnregisterManager(manager);
			engine.Stop();
		}

		public override void HandlePeerConnected(PeerId id, MonoTorrent.Common.Direction direction)
		{
			id.CloseConnection();
		}

		public override void Tick(int counter)
		{
			if (handle.WaitOne(0, true))
			{
				handle.Close();
				Manager.Mode = new StoppedMode(Manager);
				Manager.UpdateState(MonoTorrent.Common.TorrentState.Stopped);
			}
		}
	}
}
