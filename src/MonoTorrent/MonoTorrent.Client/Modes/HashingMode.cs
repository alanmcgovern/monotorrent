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
		Task hashingTask;

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

            manager.HashFails = 0;
		}

		private async Task BeginHashing()
		{
			try
			{
				for (int index = 0; index < Manager.Torrent.Pieces.Count; index++)
				{
					var hash = await Manager.Engine.DiskManager.GetHashAsync(Manager, index);
					cts.Token.ThrowIfCancellationRequested();
					var hashPassed = hash == null ? false : Manager.Torrent.Pieces.IsValid((byte[])hash, index);
					Manager.OnPieceHashed (index, hashPassed);
				}

                cts.Token.ThrowIfCancellationRequested();
                if (Manager.Mode == this)
				    await HashingComplete();
			} catch (OperationCanceledException) {

			}
		}

		private async Task HashingComplete()
		{
			Manager.HashChecked = true;

			if (Manager.Engine != null && Manager.HasMetadata && await Manager.Engine.DiskManager.CheckAnyFilesExistAsync(Manager))
				await Manager.Engine.DiskManager.CloseFilesAsync (Manager);

			cts.Token.ThrowIfCancellationRequested();

			if (autostart)
			{
				await Manager.StartAsync();
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
				if (hashingTask == null)
				{
					if (Manager.HasMetadata && await Manager.Engine.DiskManager.CheckAnyFilesExistAsync(Manager))
					{
						hashingTask = BeginHashing();
					}
					else
					{
						for (int i = 0; i < Manager.Torrent.Pieces.Count; i++)
							Manager.OnPieceHashed(i, false);
						hashingTask = HashingComplete();
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
