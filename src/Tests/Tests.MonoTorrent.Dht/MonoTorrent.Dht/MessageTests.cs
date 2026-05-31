//
// MessageTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Unicode;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Dht.Messages.Efficient;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class MessageTests
    {
        private readonly NodeId id = new NodeId (Encoding.UTF8.GetBytes ("abcdefghij0123456789").AsSpan ());
        private readonly NodeId infohash = new NodeId (Encoding.UTF8.GetBytes ("mnopqrstuvwxyz123456").AsSpan ());
        private readonly BEncodedString token = "aoeusnth";
        private readonly TransactionId transactionId = TransactionId.From ((byte) 'a', (byte) 'a');

        DhtMessageFactory DhtMessageFactory;

        [SetUp]
        public void Setup ()
        {
            DhtMessageFactory = new DhtMessageFactory ();
        }

        [TearDown]
        public void Teardown ()
        {
        }

        #region Encode Tests

        [Test]
        public void AnnouncePeerEncode ()
        {
            Node n = new Node (NodeId.Create (), default);
            n.Token = token;
            var m = KrpcMessageEncoder.EncodeAnnouncePeer (transactionId, id.Span, infohash.Span, token.Span, 6881, false);
            Compare (m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }

        [Test]
        public void AnnouncePeerEncodeImpliedPort ()
        {
            Node n = new Node (NodeId.Create (), default);
            n.Token = token;
            var m = KrpcMessageEncoder.EncodeAnnouncePeer (transactionId, id.Span, infohash.Span, token.Span, 6881, true);
            Compare (m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }

        [Test]
        public void AnnouncePeerResponseEncode ()
        {
            var m = KrpcMessageEncoder.EncodeAnnouncePeerResponse (transactionId, infohash.Span);

            Compare (m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re", QueryMethod.AnnouncePeer);
        }

        [Test]
        public void FindNodeEncode ()
        {
            var m = KrpcMessageEncoder.EncodeFindNode (transactionId, id.Span, infohash.Span);

            Compare (m, "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe");
        }

        [Test]
        public void FindNodeResponseEncode ()
        {
            var m = KrpcMessageEncoder.EncodeFindNodeResponse (transactionId, id.Span, "def456..."u8);

            Compare (m, "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re", QueryMethod.FindNode);
        }

        [Test]
        public void GetPeersEncode ()
        {
            var m = KrpcMessageEncoder.EncodeGetPeers (transactionId, id.Span, infohash.Span);

            Compare (m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe");
        }

        [Test]
        public void GetPeersResponseEncode ()
        {
            var values = new BEncodedList {
                ((BEncodedString) "axje.u"),
                ((BEncodedString) "idhtnm"),
            }.Encode ();
            var m = KrpcMessageEncoder.EncodeGetPeersResponseValues (transactionId, id.Span, token.Span, values);
            Compare (m, "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re", QueryMethod.GetPeers);
        }

        [Test]
        public void PingEncode ()
        {
            var m = KrpcMessageEncoder.EncodePing (transactionId, id.Span);

            Compare (m, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe");
        }

        [Test]
        public void PingResponseEncode ()
        {
            var m = KrpcMessageEncoder.EncodePingResponse (transactionId, infohash.Span);

            Compare (m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re", QueryMethod.Ping);
        }


        #endregion

        #region Decode Tests

        [Test]
        public void AnnouncePeerDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (m.TransactionId.SequenceEqual (transactionId), "#1");
            Assert.AreEqual (m.MessageType, KrpcType.Query, "#2");
            Assert.AreEqual (m.QueryMethod, QueryMethod.AnnouncePeer, "#2");
            Assert.IsTrue (id.Span.SequenceEqual (m.NodeId), "#3");
            Assert.IsTrue (infohash.Span.SequenceEqual (m.Request.InfoHash), "#3");
            Assert.AreEqual (6881, m.Request.Port, "#4");
            Assert.IsTrue (token.Span.SequenceEqual (m.Request.Token), "#5");

            Compare (buffer, text);
        }

        [Test]
        public void AnnouncePeerResponseDecode ()
        {
            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            var buffer = System.Text.Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);
            Assert.IsTrue (infohash.Span.SequenceEqual (m.NodeId), "#1");

            Compare (buffer, text, QueryMethod.AnnouncePeer);
        }

        [Test]
        public void FindNodeDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (id.Span.SequenceEqual (m.NodeId), "#1");
            Assert.IsTrue (infohash.Span.SequenceEqual (m.Request.Target), "#1");
            Compare (buffer, text);
        }

        [Test]
        public void FindNodeResponseDecode ()
        {
            FindNodeEncode ();
            //DhtMessageFactory.RegisterSend (message, new IPEndPoint (IPAddress.Any, 5));
            string text = "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (id.Span.SequenceEqual (m.NodeId), "#1");
            Assert.IsTrue ("def456..."u8.SequenceEqual (m.Response.Nodes), "#2");
            Assert.IsTrue (m.TransactionId.SequenceEqual (transactionId), "#3");

            Compare (buffer, text, QueryMethod.FindNode);
        }

        [Test]
        public void GetPeersDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (infohash.Span.SequenceEqual(m.Request.InfoHash), "#1");
            Assert.IsTrue (id.Span.SequenceEqual (m.NodeId), "#1");
            Assert.IsTrue (m.TransactionId.SequenceEqual (transactionId), "#3");

            Compare (buffer, text);
        }

        [Test]
        public void GetPeersResponseDecode ()
        {
            string text = "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (token.Span.SequenceEqual (m.Response.Token), "#1");
            Assert.IsTrue (id.Span.SequenceEqual (m.NodeId), "#1");

            BEncodedList l = new BEncodedList ();
            l.Add ((BEncodedString) "axje.u");
            l.Add ((BEncodedString) "idhtnm");
            Assert.IsTrue (l.Encode ().AsSpan ().SequenceEqual (m.Response.Values), "#3");

            Compare (buffer, text, QueryMethod.GetPeers);
        }

        [Test]
        public void PingDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (id.Span.SequenceEqual(m.NodeId), "#1");

            Compare (buffer, text);
        }

        [Test]
        public void PingResponseDecode ()
        {
            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";
            var buffer = Encoding.UTF8.GetBytes (text);
            var m = KrpcMessage.Parse (buffer);

            Assert.IsTrue (infohash.Span.SequenceEqual(m.NodeId));

            Compare (buffer, text, QueryMethod.Ping);
        }

        #endregion

        private void Compare (ReadOnlyMemory<byte> buffer, string expected)
            => Compare (buffer, expected, null);

        private void Compare (ReadOnlyMemory<byte> buffer, string expected, QueryMethod? query)
        {
            var m = KrpcMessage.Parse (buffer);

            ReadOnlyMemory<byte> encoded = default;
            if (m.MessageType == KrpcType.Query) {
                encoded = m.QueryMethod switch {
                    QueryMethod.AnnouncePeer => KrpcMessageEncoder.EncodeAnnouncePeer (m.TransactionId, m.NodeId, m.Request.InfoHash, m.Request.Token, m.Request.Port, m.Request.ImpliedPort),
                    QueryMethod.FindNode => KrpcMessageEncoder.EncodeFindNode (m.TransactionId, m.NodeId, m.Request.Target),
                    QueryMethod.GetPeers => KrpcMessageEncoder.EncodeGetPeers (m.TransactionId, m.NodeId, m.Request.InfoHash),
                    QueryMethod.Ping => KrpcMessageEncoder.EncodePing (m.TransactionId, m.NodeId),
                    _ => throw new NotSupportedException ()
                };
            } else if (query.HasValue) {
                encoded = query.Value switch {
                    QueryMethod.AnnouncePeer => KrpcMessageEncoder.EncodeAnnouncePeerResponse (m.TransactionId, m.NodeId),
                    QueryMethod.FindNode => KrpcMessageEncoder.EncodeFindNodeResponse (m.TransactionId, m.NodeId, m.Response.Nodes),
                    QueryMethod.GetPeers => m.Response.Nodes.Length > 0
                        ? KrpcMessageEncoder.EncodeGetPeersResponseNodes (m.TransactionId, m.NodeId, m.Response.Token, m.Response.Nodes)
                        : KrpcMessageEncoder.EncodeGetPeersResponseValues (m.TransactionId, m.NodeId, m.Response.Token, m.Response.Values),
                    QueryMethod.Ping => KrpcMessageEncoder.EncodePingResponse (m.TransactionId, m.NodeId),
                    _ => throw new NotSupportedException ()
                };
            }
            Assert.AreEqual (Encoding.UTF8.GetString (buffer.Span), Encoding.UTF8.GetString (encoded.Span));
        }
    }
}
