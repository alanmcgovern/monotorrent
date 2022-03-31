//
// ManualDhtEngine.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    public class ManualDhtEngine : IDhtEngine
    {
        public TimeSpan AnnounceInterval { get; }
        public bool Disposed { get; private set; }
        public TimeSpan MinimumAnnounceInterval { get; }
        public ITransferMonitor Monitor { get; }
        public int NodeCount => 0;
        public DhtState State { get; private set; }

        public event EventHandler<PeersFoundEventArgs> PeersFound;
        public event EventHandler StateChanged;

        public void Add (IEnumerable<ReadOnlyMemory<byte>> nodes)
        {

        }

        public void Announce (InfoHash infohash, int port)
        {

        }

        public void Dispose ()
            => Disposed = true;

        public void GetPeers (InfoHash infohash)
        {

        }

        public void RaisePeersFound (InfoHash infoHash, IList<PeerInfo> peers)
            => PeersFound?.Invoke (this, new PeersFoundEventArgs (infoHash, peers));

        public void RaiseStateChanged (DhtState newState)
        {
            State = newState;
            StateChanged?.Invoke (this, EventArgs.Empty);
        }

        public Task<ReadOnlyMemory<byte>> SaveNodesAsync ()
            => Task.FromResult (ReadOnlyMemory<byte>.Empty);

        public Task SetListenerAsync (IDhtListener listener)
        {
            return Task.CompletedTask;
        }

        public Task StartAsync ()
            => StartAsync (null);

        public Task StartAsync (ReadOnlyMemory<byte> initialNodes)
        {
            RaiseStateChanged (DhtState.Ready);
            return Task.CompletedTask;
        }

        public Task StopAsync ()
        {
            RaiseStateChanged (DhtState.NotReady);
            return Task.CompletedTask;
        }
    }
}
