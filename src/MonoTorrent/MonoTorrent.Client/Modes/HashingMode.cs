using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
	class HashingMode : Mode
	{
		CancellationTokenSource cts;

		bool autostart;
		int index = -1;

		public override TorrentState State
		{
			get { return TorrentState.Hashing; }
		}

		public HashingMode(TorrentManager manager, bool autostart)
			: base(manager)
		{
			CanAcceptConnections = false;
			cts = new CancellationTokenSource();
			this.autostart = autostart;
		}

		private async void BeginHashing()
		{
			try
			{
				for (index = 0; index < Manager.Torrent.Pieces.Count && Manager.Mode == this; index++)
				{
					var hash = await Manager.Engine.DiskManager.GetHashAsync(Manager, index);
					cts.Token.ThrowIfCancellationRequested();
					Manager.Bitfield[index] = hash == null ? false : Manager.Torrent.Pieces.IsValid((byte[])hash, index);
					Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, index, Manager.Bitfield[index]));
				}

				await HashingComplete();
			} catch (OperationCanceledException) {

			}
		}

		private async Task HashingComplete()
		{
			Manager.HashChecked = index == Manager.Torrent.Pieces.Count;

			if (Manager.HasMetadata && !Manager.HashChecked)
			{
				Manager.Bitfield.SetAll(false);
				for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
					Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
			}

			if (Manager.Engine != null && Manager.HasMetadata && await Manager.Engine.DiskManager.CheckAnyFilesExistAsync(Manager))
				await Manager.Engine.DiskManager.CloseFilesAsync (Manager);

			cts.Token.ThrowIfCancellationRequested();

			if (!Manager.HashChecked)
				return;

			if (autostart)
			{
				Manager.Start();
			}
			else
			{
				Manager.Mode = new StoppedMode(Manager);
			}
		}

		public override void HandlePeerConnected(PeerId id, Direction direction)
		{
			Manager.Engine.ConnectionManager.CleanupSocket (id);
		}

		public override async void Tick(int counter)
		{
			try
			{
				if (index == -1)
				{
					index++;

					if (Manager.HasMetadata && await Manager.Engine.DiskManager.CheckAnyFilesExistAsync(Manager))
					{
						BeginHashing();
					}
					else
					{
						Manager.Bitfield.SetAll(false);
						for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
							Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
						index = Manager.Torrent.Pieces.Count;
						await HashingComplete();
					}
				}
			} catch (OperationCanceledException)
			{

			}

			// Do nothing else while in hashing mode.
		}

		public override void Dispose ()
		{
			cts.Cancel ();
		}
	}
}
