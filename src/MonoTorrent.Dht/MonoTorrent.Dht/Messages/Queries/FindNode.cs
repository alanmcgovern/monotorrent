//
// FindNode.cs
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
    class FindNode : QueryMessage
    {
        private static BEncodedString TargetKey = "target";
        private static BEncodedString QueryName = "find_node";
        private static ResponseCreator responseCreator = delegate(BEncodedDictionary d, QueryMessage m) { return new FindNodeResponse(d, m); };

        public NodeId Target
        {
            get { return new NodeId((BEncodedString)Parameters[TargetKey]); }
        }

        public FindNode(NodeId id, NodeId target)
            : base(id, QueryName, responseCreator)
        {
            Parameters.Add(TargetKey, target.BencodedString());
        }

        public FindNode(BEncodedDictionary d)
            :base(d, responseCreator)
        {
        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            if (!base.Handle(engine, source))
                return false;

            FindNodeResponse response = new FindNodeResponse(engine.RoutingTable.LocalNode.Id);
            response.TransactionId = TransactionId;
            Node node = engine.RoutingTable.FindNode(Target);
            if (node != null)
                response.Nodes = node.CompactNode();
            else
                response.Nodes = Node.CompactNode(engine.RoutingTable.GetClosest(Target));

            engine.MessageLoop.EnqueueSend(response, source);
            return true;
        }
    }
}
