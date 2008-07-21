//
// PingResponse.cs
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
    class PingResponse : ResponseMessage
    {
        public PingResponse(NodeId id)
            : base(id)
        {

        }

        public PingResponse(BEncodedDictionary d, QueryMessage m)
            :base(d, m)
        {

        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            if (!base.Handle(engine, source))
                return false;

            Node node = engine.RoutingTable.FindNode(Id);
            if (node != null)
            {
                node.CurrentlyPinging = false;
                if (node.Bucket  != null)
                {
                    node.Bucket.LastChanged = DateTime.Now;
                    if (node.Bucket.Replacement != null)
                        node.Bucket.PingForReplace(engine);
                }
                // find node closer to itself from first node connected and closer nodes
                if (engine.Bootstrap)
                    engine.MessageLoop.EnqueueSend(new FindNode(engine.RoutingTable.LocalNode.Id, engine.RoutingTable.LocalNode.Id), node);
            }
            return true;
        }
    }
}

