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
        int activeQueries;
    	NodeId infoHash;
    	DhtEngine engine;
        System.Threading.Tasks.TaskCompletionSource<Node[]> tcs = new System.Threading.Tasks.TaskCompletionSource<Node[]> ();

        SortedList<NodeId, NodeId> ClosestNodes { get; }
        internal SortedList<NodeId, Node> ClosestActiveNodes { get; }

        public GetPeersTask(DhtEngine engine, InfoHash infohash)
            : this(engine, new NodeId(infohash))
    	{
    		
    	}

        public GetPeersTask(DhtEngine engine, NodeId infohash)
        {
            this.engine = engine;
            this.infoHash = infohash;
            this.ClosestNodes = new SortedList<NodeId, NodeId>(Bucket.MaxCapacity);
            this.ClosestActiveNodes = new SortedList<NodeId, Node>(Bucket.MaxCapacity * 2);
        }

        public async Task<Node[]> Execute ()
        {
            var newNodes = engine.RoutingTable.GetClosest (infoHash);
            foreach (Node n in Node.CloserNodes(infoHash, ClosestNodes, newNodes, Bucket.MaxCapacity))
                await SendGetPeers(n);

            return ClosestActiveNodes.Values.ToArray ();
        }

        private async Task SendGetPeers (Node target)
        {
            NodeId distance = target.Id.Xor(infoHash);
            ClosestActiveNodes.Add(distance, target);

            GetPeers m = new GetPeers(engine.LocalId, infoHash);
            activeQueries++;
            var args = await engine.SendQueryAsync (m, target);
            activeQueries--;

            // We want to keep a list of the top (K) closest nodes which have responded
            int index = ClosestActiveNodes.Values.IndexOf (target);
            if (index >= Bucket.MaxCapacity || args.Response == null)
                ClosestActiveNodes.RemoveAt (index);

            if (args.Response == null) {
                if (activeQueries == 0)
                    tcs.TrySetResult (new Node[0]);
                return;
            }

            GetPeersResponse response = (GetPeersResponse)args.Response;

            // Ensure that the local Node object has the token. There may/may not be
            // an additional copy in the routing table depending on whether or not
            // it was able to fit into the table.
            target.Token = response.Token;
            if (response.Values != null) {
                // We have actual peers!
                engine.RaisePeersFound (infoHash, Peer.Decode (response.Values));
            } else if (response.Nodes != null) {
                // We got a list of nodes which are closer
                IEnumerable<Node> newNodes = Node.FromCompactNode (response.Nodes);
                foreach (Node closer in Node.CloserNodes (infoHash, ClosestNodes, newNodes, Bucket.MaxCapacity))
                    await SendGetPeers (closer);
            }

            if (activeQueries == 0)
                tcs.TrySetResult (ClosestActiveNodes.Values.ToArray ());
        }
    }
}
