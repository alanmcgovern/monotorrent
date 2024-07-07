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
using System.Text;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;

using NUnit.Framework;

namespace MonoTorrent.Dht
{
    [TestFixture]
    public class MessageTests
    {
        private readonly NodeId id = new NodeId (Encoding.UTF8.GetBytes ("abcdefghij0123456789"));
        private readonly NodeId infohash = new NodeId (Encoding.UTF8.GetBytes ("mnopqrstuvwxyz123456"));
        private readonly BEncodedString token = "aoeusnth";
        private readonly BEncodedString transactionId = "aa";

        private QueryMessage message;
        DhtMessageFactory DhtMessageFactory;

        [SetUp]
        public void Setup ()
        {
            DhtMessage.UseVersionKey = false;
            DhtMessageFactory = new DhtMessageFactory ();
        }

        [TearDown]
        public void Teardown ()
        {
            DhtMessage.UseVersionKey = true;
        }

        #region Encode Tests

        [Test]
        public void AnnouncePeerEncode ()
        {
            Node n = new Node (NodeId.Create (), null);
            n.Token = token;
            AnnouncePeer m = new AnnouncePeer (id, infohash, 6881, token);
            m.TransactionId = transactionId;

            Compare (m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }

        [Test]
        public void AnnouncePeerResponseEncode ()
        {
            AnnouncePeerResponse m = new AnnouncePeerResponse (infohash, transactionId);

            Compare (m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        [Test]
        public void FindNodeEncode ()
        {
            FindNode m = new FindNode (id, infohash);
            m.TransactionId = transactionId;

            Compare (m, "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void FindNodeResponseEncode ()
        {
            FindNodeResponse m = new FindNodeResponse (id, transactionId);
            m.Nodes = "def456...";

            Compare (m, "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re");
        }

        [Test]
        public void GetPeersEncode ()
        {
            GetPeers m = new GetPeers (id, infohash);
            m.TransactionId = transactionId;

            Compare (m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void GetPeersResponseEncode ()
        {
            GetPeersResponse m = new GetPeersResponse (id, transactionId, token);
            m.Values = new BEncodedList ();
            m.Values.Add ((BEncodedString) "axje.u");
            m.Values.Add ((BEncodedString) "idhtnm");
            Compare (m, "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re");
        }

        [Test]
        public void PingEncode ()
        {
            Ping m = new Ping (id);
            m.TransactionId = transactionId;

            Compare (m, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void PingResponseEncode ()
        {
            PingResponse m = new PingResponse (infohash, transactionId);

            Compare (m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }


        #endregion

        #region Decode Tests

        [Test]
        public void AnnouncePeerDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe";
            AnnouncePeer m = (AnnouncePeer) Decode ("d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
            Assert.AreEqual (m.TransactionId, transactionId, "#1");
            Assert.AreEqual (m.MessageType, QueryMessage.QueryType, "#2");
            Assert.AreEqual (id, m.Id, "#3");
            Assert.AreEqual (infohash, m.InfoHash, "#3");
            Assert.AreEqual ((BEncodedNumber) 6881, m.Port, "#4");
            Assert.AreEqual (token, m.Token, "#5");

            Compare (m, text);
            message = m;
        }


        [Test]
        public void AnnouncePeerResponseDecode ()
        {
            // Register the query as being sent so we can decode the response
            AnnouncePeerDecode ();
            DhtMessageFactory.RegisterSend (message);
            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            AnnouncePeerResponse m = (AnnouncePeerResponse) Decode (text);
            Assert.AreEqual (infohash, m.Id, "#1");

            Compare (m, text);
        }

        [Test]
        public void FindNodeDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe";
            FindNode m = (FindNode) Decode (text);

            Assert.AreEqual (id, m.Id, "#1");
            Assert.AreEqual (infohash, m.Target, "#1");
            Compare (m, text);
        }

        [Test]
        public void FindNodeResponseDecode ()
        {
            FindNodeEncode ();
            DhtMessageFactory.RegisterSend (message);
            string text = "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re";
            FindNodeResponse m = (FindNodeResponse) Decode (text);

            Assert.AreEqual (id, m.Id, "#1");
            Assert.AreEqual ((BEncodedString) "def456...", m.Nodes, "#2");
            Assert.AreEqual (transactionId, m.TransactionId, "#3");

            Compare (m, text);
        }

        [Test]
        public void GetPeersDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe";
            GetPeers m = (GetPeers) Decode (text);

            Assert.AreEqual (infohash, m.InfoHash, "#1");
            Assert.AreEqual (id, m.Id, "#2");
            Assert.AreEqual (transactionId, m.TransactionId, "#3");

            Compare (m, text);
        }

        [Test]
        public void GetPeersResponseDecode ()
        {
            GetPeersEncode ();
            DhtMessageFactory.RegisterSend (message);

            string text = "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re";
            GetPeersResponse m = (GetPeersResponse) Decode (text);

            Assert.AreEqual (token, m.Token, "#1");
            Assert.AreEqual (id, m.Id, "#2");

            BEncodedList l = new BEncodedList ();
            l.Add ((BEncodedString) "axje.u");
            l.Add ((BEncodedString) "idhtnm");
            Assert.AreEqual (l, m.Values, "#3");

            Compare (m, text);
        }

        [Test]
        public void PingDecode ()
        {
            string text = "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe";
            Ping m = (Ping) Decode (text);

            Assert.AreEqual (id, m.Id, "#1");

            Compare (m, text);
        }

        [Test]
        public void PingResponseDecode ()
        {
            PingEncode ();
            DhtMessageFactory.RegisterSend (message);

            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";
            PingResponse m = (PingResponse) Decode (text);

            Assert.AreEqual (infohash, m.Id);

            Compare (m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        #endregion


        private void Compare (DhtMessage m, string expected)
        {
            ReadOnlyMemory<byte> b = m.Encode ();
            Assert.AreEqual (Encoding.UTF8.GetString (b.ToArray ()), expected);
        }

        private DhtMessage Decode (string p)
        {
            byte[] buffer = Encoding.UTF8.GetBytes (p);
            return DhtMessageFactory.DecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (buffer));
        }
    }
}
