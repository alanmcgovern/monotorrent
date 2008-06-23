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


using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.BEncoding;
using System.Net;

namespace MonoTorrent.Dht.Messages
{
    internal class GetPeers : QueryMessage
    {
        private static BEncodedString InfoHashKey = "info_hash";
        private static BEncodedString QueryName = "get_peers";
        private static Creator responseCreator = delegate(BEncodedDictionary d) { return new GetPeersResponse(d); };

        public NodeId InfoHash
        {
            get { return new NodeId((BEncodedString)Parameters[InfoHashKey]); }
        }

        public GetPeers(NodeId id, NodeId infohash)
            : base(id, QueryName, responseCreator)
        {
            Parameters.Add(InfoHashKey, infohash.BencodedString());
        }

        public GetPeers(BEncodedDictionary d)
            : base(d, responseCreator)
        {

        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            if (!base.Handle(engine, source))
                return false;

            // FIXME: Need a class which handles token generation properly
            GetPeersResponse response = new GetPeersResponse(engine.RoutingTable.LocalNode.Id, "token!");
            response.TransactionId = TransactionId;
            if (engine.Torrents.ContainsKey(InfoHash))
            {
                BEncodedList list = new BEncodedList();
                foreach (Node node in engine.Torrents[InfoHash])
                    list.Add(node.CompactPort());
                response.Values = list;
            }
            else
            {
                // Is this right?
                response.Nodes = Node.CompactNode(engine.RoutingTable.GetClosest(InfoHash));
            }

            engine.MessageLoop.EnqueueSend(response, source);
            return true;
        }
    }
}
