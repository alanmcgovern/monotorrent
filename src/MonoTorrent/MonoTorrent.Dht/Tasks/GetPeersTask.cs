using MonoTorrent.Dht.Messages;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System;
using MonoTorrent.Client;
using System.Linq;
using System.Threading.Tasks;

namespace MonoTorrent.Dht.Tasks
{
    class GetPeersTask
    {
        DhtEngine Engine { get; }
        NodeId InfoHash { get; }

        public GetPeersTask(DhtEngine engine, InfoHash infohash)
            : this(engine, new NodeId(infohash))
        {

        }

        public GetPeersTask(DhtEngine engine, NodeId infohash)
        {
            Engine = engine;
            InfoHash = infohash;
        }

        public async Task<IEnumerable<Node>> ExecuteAsync()
        {
            var activeQueries = new List<Task<SendQueryEventArgs>> ();
            var closestNodes = new ClosestNodesCollection(InfoHash);

            foreach (var node in Engine.RoutingTable.GetClosest (InfoHash)) {
                if (closestNodes.Add (node))
                    activeQueries.Add (Engine.SendQueryAsync (new GetPeers (Engine.LocalId, InfoHash), node));
            }

            while (activeQueries.Count > 0) {
                Task<SendQueryEventArgs> completed = await Task.WhenAny (activeQueries);
                activeQueries.Remove (completed);

                // If it timed out or failed just move to the next query.
                SendQueryEventArgs query = await completed;
                if (query.Response == null)
                    continue;

                var response = (GetPeersResponse) query.Response;
                // The response had some actual peers
                if (response.Values != null) {
                    // We have actual peers!
                    Engine.RaisePeersFound (InfoHash, Peer.Decode (response.Values));
                }

                // The response contains nodes which should be closer to our target. If they are closer than nodes
                // we've already checked, then let's query them!
                if (response.Nodes != null) {
                    foreach (var node in Node.FromCompactNode (response.Nodes))
                        if (closestNodes.Add (node))
                            activeQueries.Add (Engine.SendQueryAsync (new GetPeers (Engine.LocalId, InfoHash), node));
                }
            }

            // Finally, return the 8 closest nodes we discovered during this phase. These are the nodes we should
            // announce to later.
            return closestNodes;
        }
    }
}
