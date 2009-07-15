using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client
{
	class HashingMode : Mode
	{
		internal ManualResetEvent hashingWaitHandle;

		bool autostart;
		bool filesExist;
		int index = -1;

		public override bool CanAcceptConnections
		{
			get { return false; }
		}

		public HashingMode(TorrentManager manager, bool autostart)
			: base(manager)
		{
			this.hashingWaitHandle = new ManualResetEvent(false);
			this.autostart = autostart;
			this.filesExist = Manager.HasMetadata && manager.Engine.DiskManager.CheckFilesExist(Manager);
			if (!filesExist)
				HashingComplete();
		}

		private void QueueNextHash()
		{
			if (Manager.Mode != this || index == Manager.Torrent.Pieces.Count)
				HashingComplete();
			else
				Manager.Engine.DiskManager.BeginGetHash(Manager, index, PieceComplete);
		}

		private void PieceComplete(object hash)
		{
			if (Manager.Mode != this)
			{
				HashingComplete();
			}
			else
			{

				Manager.Bitfield[index] = Manager.Torrent.Pieces.IsValid((byte[])hash, index);
				Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, index, Manager.Bitfield[index]));
				index++;
				QueueNextHash();
			}
		}

		private void HashingComplete()
		{
			Manager.HashChecked = index == Manager.Torrent.Pieces.Count;

			if (Manager.HasMetadata && !Manager.HashChecked)
			{
				Manager.Bitfield.SetAll(false);
				for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
					Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
			}

			if (Manager.Engine != null && filesExist)
				Manager.Engine.DiskManager.CloseFileStreams(Manager, Manager.SavePath, Manager.Torrent.Files);

			hashingWaitHandle.Set();

			if (!Manager.HashChecked)
				return;

			if (autostart)
			{
				Manager.Start();
			}
			else
			{
				Manager.UpdateState(TorrentState.Stopped);
				Manager.Mode = new StoppedMode(Manager);
			}
		}

		public override void HandlePeerConnected(PeerId id, MonoTorrent.Common.Direction direction)
		{
			id.CloseConnection();
		}

		public override void Tick(int counter)
		{
			if (index == -1)
			{
				index++;
				QueueNextHash();
			}
			// Do nothing in hashing mode
		}
	}
}
