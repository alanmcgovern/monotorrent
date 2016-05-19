#if !DISABLE_DHT
using System;
using System.Text;
using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using Xunit;

namespace MonoTorrent.Dht
{
    public class MessageTests : IDisposable
    {
        public MessageTests()
        {
            Message.UseVersionKey = false;
        }

        public void Dispose()
        {
            Message.UseVersionKey = true;
        }

        //static void Main(string[] args)
        //{
        //    MessageTests t = new MessageTests();
        //    t.GetPeersResponseEncode();
        //}
        private readonly NodeId id = new NodeId(Encoding.UTF8.GetBytes("abcdefghij0123456789"));
        private readonly NodeId infohash = new NodeId(Encoding.UTF8.GetBytes("mnopqrstuvwxyz123456"));
        private readonly BEncodedString token = "aoeusnth";
        private readonly BEncodedString transactionId = "aa";

        private QueryMessage message;

        private void Compare(Message m, string expected)
        {
            var b = m.Encode();
            Assert.Equal(Encoding.UTF8.GetString(b), expected);
        }

        private Message Decode(string p)
        {
            var buffer = Encoding.UTF8.GetBytes(p);
            return MessageFactory.DecodeMessage(BEncodedValue.Decode<BEncodedDictionary>(buffer));
        }

        [Fact]
        public void AnnouncePeerDecode()
        {
            var text =
                "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe";
            var m =
                (AnnouncePeer)
                    Decode(
                        "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
            Assert.Equal(m.TransactionId, transactionId);
            Assert.Equal(m.MessageType, QueryMessage.QueryType);
            Assert.Equal(id, m.Id);
            Assert.Equal(infohash, m.InfoHash);
            Assert.Equal(6881, m.Port);
            Assert.Equal(token, m.Token);

            Compare(m, text);
            message = m;
        }

        [Fact]
        public void AnnouncePeerEncode()
        {
            var n = new Node(NodeId.Create(), null);
            n.Token = token;
            var m = new AnnouncePeer(id, infohash, 6881, token);
            m.TransactionId = transactionId;

            Compare(m,
                "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }


        [Fact]
        public void AnnouncePeerResponseDecode()
        {
            // Register the query as being sent so we can decode the response
            AnnouncePeerDecode();
            MessageFactory.RegisterSend(message);
            var text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            var m = (AnnouncePeerResponse) Decode(text);
            Assert.Equal(infohash, m.Id);

            Compare(m, text);
        }

        [Fact]
        public void AnnouncePeerResponseEncode()
        {
            var m = new AnnouncePeerResponse(infohash, transactionId);

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        [Fact]
        public void FindNodeDecode()
        {
            var text = "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe";
            var m = (FindNode) Decode(text);

            Assert.Equal(id, m.Id);
            Assert.Equal(infohash, m.Target);
            Compare(m, text);
        }

        [Fact]
        public void FindNodeEncode()
        {
            var m = new FindNode(id, infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe");
            message = m;
        }

        [Fact]
        public void FindNodeResponseDecode()
        {
            FindNodeEncode();
            MessageFactory.RegisterSend(message);
            var text = "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re";
            var m = (FindNodeResponse) Decode(text);

            Assert.Equal(id, m.Id);
            Assert.Equal("def456...", m.Nodes);
            Assert.Equal(transactionId, m.TransactionId);

            Compare(m, text);
        }

        [Fact]
        public void FindNodeResponseEncode()
        {
            var m = new FindNodeResponse(id, transactionId);
            m.Nodes = "def456...";

            Compare(m, "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re");
        }

        [Fact]
        public void GetPeersDecode()
        {
            var text =
                "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe";
            var m = (GetPeers) Decode(text);

            Assert.Equal(infohash, m.InfoHash);
            Assert.Equal(id, m.Id);
            Assert.Equal(transactionId, m.TransactionId);

            Compare(m, text);
        }

        [Fact]
        public void GetPeersEncode()
        {
            var m = new GetPeers(id, infohash);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe");
            message = m;
        }

        [Fact]
        public void GetPeersResponseDecode()
        {
            GetPeersEncode();
            MessageFactory.RegisterSend(message);

            var text = "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re";
            var m = (GetPeersResponse) Decode(text);

            Assert.Equal(token, m.Token);
            Assert.Equal(id, m.Id);

            var l = new BEncodedList();
            l.Add((BEncodedString) "axje.u");
            l.Add((BEncodedString) "idhtnm");
            Assert.Equal(l, m.Values);

            Compare(m, text);
        }

        [Fact]
        public void GetPeersResponseEncode()
        {
            var m = new GetPeersResponse(id, transactionId, token);
            m.Values = new BEncodedList();
            m.Values.Add((BEncodedString) "axje.u");
            m.Values.Add((BEncodedString) "idhtnm");
            Compare(m, "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re");
        }

        [Fact]
        public void PingDecode()
        {
            var text = "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe";
            var m = (Ping) Decode(text);

            Assert.Equal(id, m.Id);

            Compare(m, text);
        }

        [Fact]
        public void PingEncode()
        {
            var m = new Ping(id);
            m.TransactionId = transactionId;

            Compare(m, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe");
            message = m;
        }

        [Fact]
        public void PingResponseDecode()
        {
            PingEncode();
            MessageFactory.RegisterSend(message);

            var text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";
            var m = (PingResponse) Decode(text);

            Assert.Equal(infohash, m.Id);

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        [Fact]
        public void PingResponseEncode()
        {
            var m = new PingResponse(infohash, transactionId);

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }
    }
}

#endif