//
// HashingMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
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

		public override void HandlePeerConnected(PeerId id)
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
