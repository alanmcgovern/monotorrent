//
// StoppingMode.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Alan McGovern
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client
{
	class StoppingMode : Mode
	{
		List<Task> AnnouncingTasks { get; }
		CancellationTokenSource Cancellation { get; }
		Task StopDiskManagerTask { get; }
		TaskCompletionSource<object> StoppedCompletionSource { get; }

		internal Task StoppedTask => StoppedCompletionSource.Task;

		public override TorrentState State
		{
			get { return TorrentState.Stopping; }
		}

		public StoppingMode(TorrentManager manager)
			: base(manager)
		{
			AnnouncingTasks = new List<Task>();
			Cancellation = new CancellationTokenSource (TimeSpan.FromMinutes (1));
			StoppedCompletionSource = new TaskCompletionSource<object> ();

			// Ensure the TCS *always* gets completed.
			Cancellation.Token.Register (() => StoppedCompletionSource.TrySetCanceled ());

			CanAcceptConnections = false;
			ClientEngine engine = manager.Engine;

			if (manager.TrackerManager.CurrentTracker != null && manager.TrackerManager.CurrentTracker.Status == TrackerState.Ok)
				AnnouncingTasks.Add(manager.TrackerManager.Announce(TorrentEvent.Stopped));

			foreach (PeerId id in manager.Peers.ConnectedPeers)
				if (id.Connection != null)
					id.Connection.Dispose();

			manager.Peers.ClearAll();

			StopDiskManagerTask = engine.DiskManager.CloseFilesAsync (manager);

			manager.Monitor.Reset();
			manager.PieceManager.Reset();
			engine.ConnectionManager.CancelPendingConnects (manager);
			engine.Stop();
		}

		public override void HandlePeerConnected(PeerId id)
		{
			Manager.Engine.ConnectionManager.CleanupSocket (id);
		}

		public override void Tick(int counter)
		{
			if (AnnouncingTasks.TrueForAll (t => t.IsCompleted) && StopDiskManagerTask.IsCompleted) {
				Manager.Mode = new StoppedMode(Manager);
				StoppedCompletionSource.TrySetResult (null);
			}
		}
	}
}
