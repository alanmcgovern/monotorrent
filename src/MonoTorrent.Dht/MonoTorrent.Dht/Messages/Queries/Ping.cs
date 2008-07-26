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
        private static ResponseCreator responseCreator = delegate(BEncodedDictionary d, QueryMessage m) { return new PingResponse(d, m); };

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
            if (!base.Handle(engine, source))
                return false;

            PingResponse m = new PingResponse(engine.RoutingTable.LocalNode.Id);
            m.TransactionId = TransactionId;
            engine.MessageLoop.EnqueueSend(m, source);
            return true;
        }

        public override bool TimedOut(DhtEngine engine)
        {
            if (!base.TimedOut(engine))
                return false;

            Node n = engine.RoutingTable.FindNode(this.Id);
            if (n == null)
                return false;
            
            // FIXME: This should probably be handled in a Ping task (or something)
            // Idea - Do something like this:

            // 1) To ping a node, create a SendMessageTask
            // 2) This task will have a constructor like: public SendMessageTask (Node targetNode, QueryMessage message)
            // 3) If a response is not received, the message should time out immediately, a resend will not be attempted
            // 4) The SendMessageTask can have a configurable number of retries and it will
            //    decide if a resend should be attempted or not.

            // The code below is commented out while i figure out where the best place to put it is...
            // probably in a SendMessageTask type of thing


            //if become bad else base will ping again
            //if (n.Bucket != null && n.Bucket.Replacement != null && n.State == NodeState.Bad)
            //    n.Bucket.Replace (n); 

            return true;
        }
    }
}
