//
// Ping.cs
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
    internal class Ping : QueryMessage
    {
        private static BEncodedString QueryName = "ping";
        private static Creator responseCreator = delegate(BEncodedDictionary d) { return new PingResponse(d); };

        public Ping(NodeId id)
            :base(id, QueryName, responseCreator)
        {

        }

        public Ping(BEncodedDictionary d)
            : base(d, responseCreator)
        {

        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            Node node = engine.RoutingTable.FindNode(Id);
            
            if (node == null)
                return false;

            node.Seen();

            PingResponse m = new PingResponse(engine.RoutingTable.LocalNode.Id);
            engine.MessageLoop.EnqueueSend(m, source);
            return true;
        }

        public override void TimedOut(DhtEngine engine)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
