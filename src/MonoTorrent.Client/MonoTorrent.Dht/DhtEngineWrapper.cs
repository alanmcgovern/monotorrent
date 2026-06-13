//
// DhtEngineWrapper.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2022 Alan McGovern
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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;
using MonoTorrent.Logging;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class DhtEngineWrapper : IDht, IDhtAnnounceScheduler
    {
        static readonly Logger Log = Logger.Create (nameof (DhtEngineWrapper));

        readonly SemaphoreSlim AnnounceConcurrency = new SemaphoreSlim (20, 20);
        readonly SemaphoreSlim GetPeersConcurrency = new SemaphoreSlim (20, 20);

        IDhtEngine Engine { get; }

        public EventHandler<PeersFoundEventArgs>? PeersFound;
        public event EventHandler? StateChanged;

        public ITransferMonitor Monitor => Engine.Monitor;
        public int NodeCount => Engine.NodeCount;
        public DhtState State => Engine.State;
        public TimeSpan AnnounceInterval => Engine.AnnounceInterval;
        public TimeSpan MinimumAnnounceInterval => Engine.MinimumAnnounceInterval;

        public DhtEngineWrapper (IDhtEngine engine)
        {
            Engine = engine;
            Engine.StateChanged += (o, e) => StateChanged?.Invoke (this, e);
            engine.PeersFound += (o, e) => PeersFound?.Invoke (this, e);
        }

        public bool TryEnqueueAnnounce (InfoHashes infoHashes, int port, CancellationToken token)
        {
            if (infoHashes is null)
                throw new ArgumentNullException (nameof (infoHashes));
            if (State != DhtState.Ready)
                throw new InvalidOperationException ("You cannot 'Announce' while the dht table is initialising");

            if (!AnnounceConcurrency.Wait (0))
                return false;

            static async void Announce (IDhtEngine engine, SemaphoreSlim semaphore, InfoHashes infoHashes, int port, CancellationToken token)
            {
                try {
                    // Announce to both infohashes if they exist.
                    var v1 = engine.AnnounceAsync (infoHashes.V1OrV2.Truncate (), port);
                    try {
                        if (infoHashes.IsHybrid)
                            await engine.AnnounceAsync (infoHashes.V2.Truncate (), port);
                    } finally {
                        await v1;
                    }
                } catch (Exception ex) {
                    if (ex.GetBaseException () is not OperationCanceledException op || op.CancellationToken != token) {
                        Log.Exception (ex, "Unexpected failure announcing an infohash to the DhtEngine");
                    }
                } finally {
                    semaphore.Release ();
                }
            }

            Announce (Engine, AnnounceConcurrency, infoHashes, port, token);
            return true;
        }

        public bool TryEnqueueGetPeers (InfoHashes infoHashes, CancellationToken token)
        {
            if (infoHashes == null)
                throw new ArgumentNullException (nameof (infoHashes));
            if (State != DhtState.Ready)
                throw new InvalidOperationException ("You cannot 'GetPeers' while the dht table is initialising");

            if (!GetPeersConcurrency.Wait (0))
                return false;

            static async void GetPeers (IDhtEngine engine, SemaphoreSlim semaphore, InfoHashes infoHashes)
            {
                try {
                    // Run 'GetPeers' on both infohashes if they exist
                    var t1 = engine.GetPeersAsync (infoHashes.V1OrV2.Truncate ());
                    try {
                        if (infoHashes.IsHybrid)
                            await engine.GetPeersAsync (infoHashes.V2.Truncate ());
                    } finally {
                        await t1;
                    }
                } finally {
                    semaphore.Release ();
                }
            }
            GetPeers (Engine, GetPeersConcurrency, infoHashes);
            return true;
        }

        internal ReusableTask SetListenerAsync (IDhtListener dhtListener)
            => Engine.SetListenerAsync (dhtListener);

        internal void Dispose ()
            => Engine.Dispose ();

        internal ReusableTask StopAsync ()
            => Engine.StopAsync ();

        internal ReusableTask StartAsync (ReadOnlyMemory<byte> readOnlyMemory)
            => Engine.StartAsync ();

        internal ReusableTask AddAsync (IEnumerable<ReadOnlyMemory<byte>> enumerable)
            => Engine.AddAsync (enumerable);

        internal ReusableTask<ReadOnlyMemory<byte>> SaveNodesAsync ()
            => Engine.SaveNodesAsync ();

        internal ReusableTask SetBootstrapRoutersAsync (IList<BootstrapRouter> routers)
            => Engine.SetBootstrapRoutersAsync (routers);
    }
}
