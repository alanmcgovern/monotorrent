//
// ErrorMode.cs
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
using System.Collections.Generic;
using System.Threading;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client.Modes
{
    class ErrorMode : IMode
    {
        public bool CanAcceptConnections => false;
        public bool CanHandleMessages => false;
        public bool CanHashCheck => true;
        public TorrentState State => TorrentState.Error;
        public CancellationToken Token => Cancellation.Token;

        CancellationTokenSource Cancellation { get; }
        ConnectionManager ConnectionManager { get; }
        TorrentManager Manager { get; }
        bool RanOnce { get; set; }

        public ErrorMode (TorrentManager manager, ConnectionManager connectionManager)
            => (Cancellation, Manager, ConnectionManager) = (new CancellationTokenSource (), manager, connectionManager);

        public void Dispose ()
            => Cancellation.Cancel ();

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
            // Nothing special needs to happen when a peer disconnects now.
        }

        public bool ShouldConnect (Peer peer)
            => false;

        public void Tick (int counter)
        {
            if (RanOnce)
                return;

            RanOnce = true;
            Manager.SetNeedsHashCheck ();
            Manager.Monitor.Reset ();
            foreach (PeerId id in new List<PeerId> (Manager.Peers.ConnectedPeers))
                ConnectionManager.CleanupSocket (Manager, id);
        }
    }
}
