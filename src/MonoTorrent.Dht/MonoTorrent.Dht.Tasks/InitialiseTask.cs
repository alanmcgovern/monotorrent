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


using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class InitialiseTask
    {
        static readonly string[] DefaultBootstrapRouters = new[] {
            "router.bittorrent.com",
            "router.utorrent.com",
            "dht.transmissionbt.com",
            "dht.aelitis.com",
            "dht.libtorrent.org:25401",
            "router.silotis.us"
        };

        // Choose a completely arbitrary value here. If we have at least this many
        // nodes in the routing table we can consider it 'healthy' enough to allow
        // the state to change to 'Ready' so torrents can begin searching for peers
        const int MinHealthyNodes = 4;

        readonly List<Node> initialNodes;
        readonly DhtEngine engine;
        readonly TaskCompletionSource<object?> initializationComplete;

        static Node[]? BootstrapNodes { get; set; }
        string[] BootstrapRouters { get; set; }

        public InitialiseTask (DhtEngine engine)
            : this (engine, Enumerable.Empty<Node> (), DefaultBootstrapRouters)
        {

        }

        public InitialiseTask (DhtEngine engine, IEnumerable<Node> nodes, string[] bootstrapRouters)
        {
            this.engine = engine;
            initialNodes = nodes.ToList ();
            BootstrapRouters = bootstrapRouters.Length == 0 ? DefaultBootstrapRouters : bootstrapRouters.ToArray ();
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
                    await SendFindNode (initialNodes);
                } else {
                    try {
                        if (BootstrapNodes is null)
                            BootstrapNodes = await GenerateBootstrapNodes ();
                        await SendFindNode (BootstrapNodes);
                    } catch {
                        initializationComplete.TrySetResult (null);
                        return;
                    }
                }
            } finally {
                initializationComplete.TrySetResult (null);
            }
        }

        async Task<Node[]> GenerateBootstrapNodes ()
        {
            // Parse the bootstrap routers into a dictionary of address/port pairs
            var addressPortDictionary = new Dictionary<string, int> ();
            foreach (string router in BootstrapRouters) {
                string[] parts = router.Split (':');
                string address = parts[0];
                int port = parts.Length == 2 ? int.Parse (parts[1]) : 6881;
                addressPortDictionary[address] = port;
            }
            // Resolve the IP addresses of the bootstrap routers
            var nodes = new ConcurrentBag<Node> ();
            await Task.WhenAll (addressPortDictionary.Select (async address => {
                try {
                    var ipAddresses = await Dns.GetHostAddressesAsync (address.Key);
                    foreach (var ip in ipAddresses)
                        nodes.Add (new Node (NodeId.Create (), new IPEndPoint (ip, address.Value)));
                } catch {
                    // Ignore any failures
                }
            }));
            return nodes.ToArray ();
        }

        async Task SendFindNode (IEnumerable<Node> newNodes)
        {
            var activeRequests = new List<Task<SendQueryEventArgs>> ();
            var nodes = new ClosestNodesCollection (engine.LocalId);

            foreach (Node node in newNodes) {
                var request = new FindNode (engine.LocalId, engine.LocalId);
                activeRequests.Add (engine.SendQueryAsync (request, node));
                nodes.Add (node);
            }

            while (activeRequests.Count > 0) {
                var completed = await Task.WhenAny (activeRequests);
                activeRequests.Remove (completed);

                SendQueryEventArgs args = await completed;
                if (args.Response != null) {
                    if (engine.RoutingTable.CountNodes () >= MinHealthyNodes)
                        initializationComplete.TrySetResult (null);

                    var response = (FindNodeResponse) args.Response;
                    foreach (Node node in Node.FromCompactNode (response.Nodes)) {
                        if (nodes.Add (node)) {
                            var request = new FindNode (engine.LocalId, engine.LocalId);
                            activeRequests.Add (engine.SendQueryAsync (request, node));
                        }
                    }
                }
            }

            if (initialNodes.Count > 0 && engine.RoutingTable.NeedsBootstrap)
                await new InitialiseTask (engine).ExecuteAsync ();
        }
    }
}
