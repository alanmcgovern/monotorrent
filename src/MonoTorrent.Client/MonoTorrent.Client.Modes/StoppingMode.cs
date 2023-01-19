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

using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client.Modes
{
    class StoppingMode : IMode
    {
        static readonly Logger logger = Logger.Create (nameof (StoppingMode));

        public bool CanAcceptConnections => false;
        public bool CanHandleMessages => false;
        public bool CanHashCheck => false;
        public TorrentState State => TorrentState.Stopping;
        public CancellationToken Token => Cancellation.Token;

        CancellationTokenSource Cancellation { get; }
        ConnectionManager ConnectionManager { get; }
        DiskManager DiskManager { get; }
        TorrentManager Manager { get; }

        public StoppingMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager)
            => (Cancellation, Manager, DiskManager, ConnectionManager) = (new CancellationTokenSource (), manager, diskManager, connectionManager);

        public void Dispose ()
            => Cancellation.Dispose ();

        public void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
        {
            // Nothing
        }

        public void HandleMessage (PeerId id, PeerMessage message, PeerMessage.Releaser releaser)
            => throw new NotSupportedException ();

        public void HandlePeerConnected (PeerId id)
            => throw new NotSupportedException ();

        public void HandlePeerDisconnected (PeerId id)
        {
            // Peers which are disconnected at this point don't need any extra cleanup
        }

        public bool ShouldConnect (Peer peer)
            => false;

        public void Tick (int counter)
        {
        }

        public async Task WaitForStoppingToComplete (TimeSpan timeout)
        {
            try {
                ConnectionManager.CancelPendingConnects (Manager);
                foreach (PeerId id in new List<PeerId> (Manager.Peers.ConnectedPeers))
                    ConnectionManager.CleanupSocket (Manager, id);

                Manager.Monitor.Reset ();
                Manager.PieceManager.Initialise ();
                Manager.finishedPieces.Clear ();

                var stoppingTasks = new List<Task> ();
                // We could be in metadata download mode
                if (Manager.Torrent != null)
                    stoppingTasks.Add (DiskManager.CloseFilesAsync (Manager));

                Task announceTask = Manager.TrackerManager.AnnounceAsync (TorrentEvent.Stopped, CancellationToken.None).AsTask ();
                if (timeout != Timeout.InfiniteTimeSpan)
                    announceTask = Task.WhenAny (announceTask, Task.Delay (timeout));
                stoppingTasks.Add (announceTask);

                // FIXME: Expose CancellationToken throughout this API.
                var delayTask = Task.Delay (TimeSpan.FromMinutes (1), Cancellation.Token);
                var overallTasks = Task.WhenAll (stoppingTasks);
                if (await Task.WhenAny (overallTasks, delayTask) == delayTask)
                    logger.Error ("Timed out waiting for the announce request to complete");
                else
                    await overallTasks;
            } catch (Exception ex) {
                logger.Exception (ex, "Unexpected exception while stopping");
            }
        }
    }
}
