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
using System.Collections.Immutable;
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

using ReusableTasks;

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
        public ImmutableHashSet<BootstrapRouter> BootstrapRouters { get; } = ImmutableHashSet<BootstrapRouter>.Empty;

        public event EventHandler<PeersFoundEventArgs> PeersFound;
        public event EventHandler StateChanged;

        public ReusableTask AddAsync (IEnumerable<ReadOnlyMemory<byte>> nodes)
        {
            return default;
        }

        public ReusableTask AnnounceAsync (InfoHash infohash, int port)
        {
            return default;
        }

        public void Dispose ()
            => Disposed = true;

        public ReusableTask GetPeersAsync (InfoHash infohash)
        {
            return default;
        }

        public void RaisePeersFound (InfoHash infoHash, IList<PeerInfo> peers)
            => PeersFound?.Invoke (this, new PeersFoundEventArgs (infoHash, peers));

        public void RaiseStateChanged (DhtState newState)
        {
            State = newState;
            StateChanged?.Invoke (this, EventArgs.Empty);
        }

        public ReusableTask<ReadOnlyMemory<byte>> SaveNodesAsync ()
            => ReusableTask.FromResult (ReadOnlyMemory<byte>.Empty);

        public ReusableTask SetBootstrapRoutersAsync (IEnumerable<BootstrapRouter> routers)
        {
            return default;
        }

        public ReusableTask SetListenerAsync (IDhtListener listener)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask StartAsync ()
            => StartAsync (null);

        public ReusableTask StartAsync (ReadOnlyMemory<byte> initialNodes)
        {
            RaiseStateChanged (DhtState.Ready);
            return ReusableTask.CompletedTask;
        }

        public ReusableTask StopAsync ()
        {
            RaiseStateChanged (DhtState.NotReady);
            return ReusableTask.CompletedTask;
        }
    }
}
