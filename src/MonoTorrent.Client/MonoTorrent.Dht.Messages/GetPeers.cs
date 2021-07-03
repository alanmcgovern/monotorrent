//
// GetPeers.cs
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


using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    sealed class GetPeers : QueryMessage
    {
        static readonly BEncodedString InfoHashKey = "info_hash";
        static readonly BEncodedString QueryName = "get_peers";

        public NodeId InfoHash => new NodeId ((BEncodedString) Parameters[InfoHashKey]);

        public GetPeers (NodeId id, NodeId infohash)
            : base (id, QueryName)
        {
            Parameters.Add (InfoHashKey, infohash.BencodedString ());
        }

        public GetPeers (BEncodedDictionary d)
            : base (d)
        {

        }

        public override ResponseMessage CreateResponse (BEncodedDictionary parameters)
        {
            return new GetPeersResponse (parameters);
        }

        public override void Handle (DhtEngine engine, Node node)
        {
            base.Handle (engine, node);

            BEncodedString token = engine.TokenManager.GenerateToken (node);
            var response = new GetPeersResponse (engine.RoutingTable.LocalNode.Id, TransactionId, token);
            if (engine.Torrents.ContainsKey (InfoHash)) {
                var list = new BEncodedList ();
                foreach (Node n in engine.Torrents[InfoHash])
                    list.Add (n.CompactPort ());
                response.Values = list;
            } else {
                response.Nodes = Node.CompactNode (engine.RoutingTable.GetClosest (InfoHash));
            }

            engine.MessageLoop.EnqueueSend (response, node, node.EndPoint);
        }
    }
}
