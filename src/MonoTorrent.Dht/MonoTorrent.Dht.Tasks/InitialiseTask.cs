//
// InitialiseTask.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Logging;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask
    {
        static readonly ILogger Logger = LoggerFactory.Create (nameof (InitialiseTask));

        // Choose a completely arbitrary value here. If we have at least this many
        // nodes in the routing table we can consider it 'healthy' enough to allow
        // the state to change to 'Ready' so torrents can begin searching for peers
        const int MinHealthyNodes = 32;

        readonly List<Node> initialNodes;
        readonly DhtEngine engine;
        readonly TaskCompletionSource<object?> initializationComplete;
        BootstrapRouter[] BootstrapRouters { get; set; }

        public InitialiseTask (DhtEngine engine)
            : this (engine, Enumerable.Empty<Node> ())
        {

        }

        public InitialiseTask (DhtEngine engine, IEnumerable<Node> nodes)
        {
            this.engine = engine;
            initialNodes = nodes.ToList ();
            BootstrapRouters = engine.BootstrapRouters.ToArray ();
            initializationComplete = new TaskCompletionSource<object?> ();
        }

        public Task ExecuteAsync ()
        {
            DhtEngine.MainLoop.CheckThread ();
            BeginAsyncInit ();
            return initializationComplete.Task;
        }

        async void BeginAsyncInit ()
        {
            // If we were given a list of nodes to load at the start, use them
            try {
                if (initialNodes.Count > 0) {
                    Logger.Info (string.Format ("Poplating with {0} initial nodes", initialNodes.Count));
                    await PopulateFirstNodes (initialNodes);
                } else {
                    try {
                        var bootstrapRouters = await GenerateBootstrapNodes (BootstrapRouters);
                        foreach (var v in bootstrapRouters)
                            engine.RoutingTable.AddIgnoredEndpoint (v.EndPoint);

                        Logger.Info (string.Format ("Populating with {0} bootstrap routers", bootstrapRouters.Length));
                        await PopulateFirstNodes (bootstrapRouters);
                    } catch (Exception ex) {
                        if (initializationComplete.TrySetResult (null))
                            Logger.Info (string.Format ("Unexpected error initiating task: {0}", ex));
                        return;
                    }
                }
            } finally {
                if (initializationComplete.TrySetResult (null))
                    Logger.Info ("Initialization complete");
            }
        }

        static async Task<Node[]> GenerateBootstrapNodes (BootstrapRouter[] bootstrapRouters)
        {
            static async Task<CompactEndPoint[]> ResolveRouter (BootstrapRouter router)
            {
                var addresses = await Dns.GetHostAddressesAsync (router.Host, AddressFamily.InterNetwork);
                return addresses.Select (t => new CompactEndPoint (t, router.Port))
                    .ToArray ();
            }

            var results = new List<Node> ();
            var tasks = bootstrapRouters.Select (ResolveRouter).ToList ();
            while (tasks.Count > 0) {
                var completed = await Task.WhenAny (tasks);
                tasks.Remove (completed);

                try {
                    var endpoints = await completed;
                    foreach (var endpoint in endpoints)
                        results.Add (new Node (NodeId.Create (), endpoint));
                } catch (Exception ex) {
                    Logger.Error (string.Format ("Could not resolve IPs for a bootstrap router: {0}", ex));
                }
            }

            return results.ToArray ();
        }

        async Task PopulateFirstNodes (IEnumerable<Node> newNodes)
        {
            // Bootstrapping approach:
            // 1) Query the bootstrap nodes/routers for nodes close to the local ID.
            // 2) Ping each of these to see if they're alive. If they respond they'll be inserted into the routing table.
            //    Up to 32 nodes will be stored in the first bucket. If the bucket splits then each will be allowed store 16. Further splits will use the standard 8 node limit.
            // 3) Pre-split the routing table so it has at least 20 buckets to ensure there is reasonable diversity across the id space. The up-to-32 initial nodes will distribute appropriately.
            // 4) Querying each bucket for a random node in each buckets range. Stop once the bucket has > 4 nodes.
            var pendingFindNodes = 0;
            var findNodesChannel = Channel.CreateUnbounded<SendQueryEventArgs> (new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });

            // Query for the first set of nodes close to our local id. Query *all* of them.
            foreach (Node node in newNodes) {
                var transactionId = TransactionId.NextId ();
                var request = KrpcMessageEncoder.EncodeFindNode (transactionId, engine.LocalId, engine.LocalId);
                engine.SendQueryAsync (request, node, findNodesChannel.Writer);
                pendingFindNodes++;
            }

            var pendingPings = 0;
            var pingsChannel = Channel.CreateUnbounded<SendQueryEventArgs> (new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
            // For each initial node or bootstrap router which responds, ping each of the provided nodes to see if they're alive.
            while (pendingFindNodes > 0) {
                SendQueryEventArgs args = await findNodesChannel.Reader.ReadAsync ();
                pendingFindNodes--;
                Logger.Info (string.Format ("{0} initial find nodes pending", pendingFindNodes));
                if (!args.Response.IsEmpty) {
                    // Ping each node we get back to ensure it's alive/reachable. If it responds
                    // it'll be in the table.
                    var response = KrpcMessage.Parse (args.Response);
                    foreach (Node node in Node.FromCompactNodes (response.Response.Nodes)) {
                        var id = TransactionId.NextId ();
                        var request = KrpcMessageEncoder.EncodePing (id, engine.LocalId);
                        engine.SendQueryAsync (request, node, pingsChannel.Writer);
                        pendingPings++;
                    }
                }
            }

            // Wait for all the pings to finish (a response is received or it times out). Should take < 10 seconds.
            while (pendingPings > 0) {
                await pingsChannel.Reader.ReadAsync ();
                pendingPings--;
                Logger.Info (string.Format ("{0} initial pings pending", pendingPings));
            }

            // If the routing table doesn't have at least 4 active nodes at this point and we launched using a cached list of dht nodes,
            // retry bootstrapping using the DHT routers.
            if (initialNodes.Count > 0 && engine.RoutingTable.NeedsBootstrap && BootstrapRouters.Length > 0) {
                Logger.Info (string.Format ("Only have {0} nodes, rebootstrapping", engine.RoutingTable.CountNodes ()));
                await new InitialiseTask (engine).ExecuteAsync ();
            } else {
                // Otherwise presplit the routing table and populate each bucket.
                engine.RoutingTable.PreSplitBuckets (20);

                // For each bucket search for a random id
                List<Task> prefillTasks = new List<Task> ();
                foreach (var bucket in engine.RoutingTable.Buckets) {
                    prefillTasks.Add (Prefill (NodeId.RandomBetween (bucket.Min, bucket.Max)));
                }
                Logger.Info (string.Format ("Started pressplits"));
                await Task.WhenAll (prefillTasks);
            }
        }

        async Task Prefill (NodeId target)
        {
            var closestNodes = new ClosestNodesCollection (target);
            var pendingFindNodes = 0;
            var findNodesChannel = Channel.CreateUnbounded<SendQueryEventArgs> (new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });

            // Query for the first set of nodes close to our local id.
            foreach (Node node in engine.RoutingTable.GetClosest (target)) {
                var transactionId = TransactionId.NextId ();
                var request = KrpcMessageEncoder.EncodeFindNode (transactionId, engine.LocalId, target.Span);
                engine.SendQueryAsync (request, node, findNodesChannel.Writer);
                pendingFindNodes++;
            }

            Logger.Info (string.Format ("{1}: Sent {0} pre-split findnodes", pendingFindNodes, Convert.ToHexString (target.Span)));

            while (pendingFindNodes > 0) {
                SendQueryEventArgs args = await findNodesChannel.Reader.ReadAsync ();
                pendingFindNodes--;
                Logger.Info (string.Format ("{1}: pending {0} pre-split findnodes", pendingFindNodes, Convert.ToHexString (target.Span)));

                if (!args.Response.IsEmpty) {
                    if (engine.RoutingTable.CountNodes () >= MinHealthyNodes)
                        initializationComplete.TrySetResult (null);

                    var response = KrpcMessage.Parse (args.Response);
                    foreach (Node node in Node.FromCompactNodes (response.Response.Nodes)) {
                        if (closestNodes.Add (node)) {
                            var id = TransactionId.NextId ();
                            var request = KrpcMessageEncoder.EncodeFindNode (id, engine.LocalId, target.Span);
                            engine.SendQueryAsync (request, node, findNodesChannel.Writer);
                            pendingFindNodes++;
                        }
                    }
                }
            }
        }
    }
}
