using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Dht.Messages;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent.Dht.Tests
{
    [TestFixture]
    public class MessageTests
    {
        private NodeId id = new NodeId(new BigInteger(Encoding.UTF8.GetBytes("abcdefghij0123456789")));
        private NodeId infohash = new NodeId(new BigInteger(Encoding.UTF8.GetBytes("mnopqrstuvwxyz123456")));
        private BEncodedString token = "aoeusnth";
        private BEncodedString transactionId = "aa";

        private QueryMessage message;

        #region Encode Tests

        [Test]
        public void AnnouncePeerEncode()
        {
            Node n = new MonoTorrent.Dht.Node(NodeId.Create(), null);
            n.Token = token;
            AnnouncePeer m = new AnnouncePeer(id, infohash, 6881, token);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }

        [Test]
        public void AnnouncePeerResponseEncode()
        {
            AnnouncePeerResponse m = new AnnouncePeerResponse(infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        [Test]
        public void FindNodeEncode()
        {
            FindNode m = new FindNode(id, infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void FindNodeResponseEncode()
        {
            FindNodeResponse m = new FindNodeResponse(id);
            m.Nodes = "def456...";
            m.TransactionId = transactionId;

            Compare(m, "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re");
        }

        [Test]
        public void GetPeersEncode()
        {
            GetPeers m = new GetPeers(id, infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void GetPeersResponseEncode()
        {
            GetPeersResponse m = new GetPeersResponse(id, token);
            m.TransactionId = transactionId;
            m.Values.Add((BEncodedString)"axje.u");
            m.Values.Add((BEncodedString)"idhtnm");
            Compare(m, "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re");
        }

        [Test]
        public void PingEncode()
        {
            Ping m = new Ping(id);
            m.TransactionId = transactionId;
            
            Compare(m, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe");
            message = m;
        }

        [Test]
        public void PingResponseEncode()
        {
            PingResponse m = new PingResponse(infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }


        #endregion

        #region Decode Tests

        [Test]
        public void AnnouncePeerDecode()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe";
            AnnouncePeer m = (AnnouncePeer)Decode("d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
            Assert.AreEqual(m.TransactionId, transactionId, "#1");
            Assert.AreEqual(m.MessageType, QueryMessage.QueryType, "#2");
            Assert.AreEqual(id, m.Id, "#3");
            Assert.AreEqual(infohash, m.InfoHash, "#3");
            Assert.AreEqual((BEncodedNumber)6881, m.Port, "#4");
            Assert.AreEqual(token, m.Token, "#5");

            Compare(m, text);
            message = m;
        }


        [Test]
        public void AnnouncePeerResponseDecode()
        {
            // Register the query as being sent so we can decode the response
            AnnouncePeerDecode();
            MessageFactory.RegisterSend(message);
            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            AnnouncePeerResponse m = (AnnouncePeerResponse)Decode(text);
            Assert.AreEqual(infohash, m.Id, "#1");

            Compare(m, text);
        }

        [Test]
        public void FindNodeDecode()
        {
            string text ="d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe";
            FindNode m = (FindNode)Decode(text);

            Assert.AreEqual(id, m.Id, "#1");
            Assert.AreEqual(infohash, m.Target, "#1");
            Compare(m, text);
        }

        [Test]
        public void FindNodeResponseDecode()
        {
            FindNodeEncode();
            MessageFactory.RegisterSend(message);
            string text = "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re";
            FindNodeResponse m = (FindNodeResponse)Decode(text);

            Assert.AreEqual(id, m.Id, "#1");
            Assert.AreEqual((BEncodedString)"def456...", m.Nodes, "#2");
            Assert.AreEqual(transactionId, m.TransactionId, "#3");

            Compare(m, text);
        }

        [Test]
        public void GetPeersDecode()
        {
            string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe";
            GetPeers m = (GetPeers)Decode(text);

            Assert.AreEqual(infohash, m.InfoHash, "#1");
            Assert.AreEqual(id, m.Id, "#2");
            Assert.AreEqual(transactionId, m.TransactionId, "#3");

            Compare(m, text);
        }

        [Test]
        public void GetPeersResponseDecode()
        {
            GetPeersEncode();
            MessageFactory.RegisterSend(message);

            string text = "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re";
            GetPeersResponse m = (GetPeersResponse)Decode(text);

            Assert.AreEqual(token, m.Token, "#1");
            Assert.AreEqual(id, m.Id, "#2");

            BEncodedList l = new BEncodedList();
            l.Add((BEncodedString)"axje.u");
            l.Add((BEncodedString)"idhtnm");
            Assert.AreEqual(l, m.Values, "#3");

            Compare(m, text);
        }

        [Test]
        public void PingDecode()
        {
            string text = "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe";
            Ping m = (Ping) Decode(text);

            Assert.AreEqual(id, m.Id, "#1");

            Compare(m, text);
        }

        [Test]
        public void PingResponseDecode()
        {
            PingEncode();
            MessageFactory.RegisterSend(message);

            string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";
            PingResponse m = (PingResponse)Decode(text);

            Assert.AreEqual(infohash, m.Id);

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        #endregion


        private void Compare(Message m, string expected)
        {
            byte[] b = m.Encode();
            Assert.AreEqual(Encoding.UTF8.GetString(b), expected);
        }

        private Message Decode(string p)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(p);
            return MessageFactory.DecodeMessage(BEncodedValue.Decode<BEncodedDictionary>(buffer));
        }
    }
}
