using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Trackers
{
    [TestFixture]
    public class PeerDecoderTests
    {
        private List<PeerInfo> peers;

        [SetUp]
        public void Setup ()
        {
            peers = new List<PeerInfo> ();
            for (int i = 0; i < 10; i++) {
                Uri uri = new Uri ($"ipv4://192.168.0.{i}:1");
                peers.Add (new PeerInfo (uri, new string (i.ToString ()[0], 20)));
            }
            peers.Add (new PeerInfo (new Uri ("ipv4://255.255.255.255:6530"), new string ('a', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://255.0.0.0:123"), new string ('b', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://0.0.255.0:312"), new string ('c', 20)));
            peers.Add (new PeerInfo (new Uri ("ipv4://0.0.0.255:3454"), new string ('a', 20)));
        }

        [Test]
        public void CorruptDictionary ()
        {
            BEncodedList l = new BEncodedList {
                new BEncodedDictionary ()
            };
            IList<PeerInfo> decoded = PeerDecoder.Decode (l, AddressFamily.InterNetwork);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptList ()
        {
            BEncodedList list = new BEncodedList ();
            for (int i = 0; i < peers.Count; i++)
                list.Add ((BEncodedString) peers[i].CompactPeer ());

            list.Insert (2, new BEncodedNumber (5));
            VerifyDecodedPeers (PeerDecoder.Decode (list, AddressFamily.InterNetwork));

            list.Clear ();
            list.Add (new BEncodedString (new byte[3]));
            IList<PeerInfo> decoded = PeerDecoder.Decode (list, AddressFamily.InterNetwork);
            Assert.AreEqual (0, decoded.Count, "#1");
        }

        [Test]
        public void DecodeList ()
        {
            // List of String
            BEncodedList list = new BEncodedList ();
            foreach (PeerInfo p in peers)
                list.Add ((BEncodedString) p.CompactPeer ());

            VerifyDecodedPeers (PeerDecoder.Decode (list, AddressFamily.InterNetwork));
        }

        [Test]
        public void DecodeDictionary ()
        {
            var list = new BEncodedList ();
            foreach (PeerInfo p in peers) {
                var dict = new BEncodedDictionary {
                    {"ip", (BEncodedString) p.ConnectionUri.Host},
                    {"port", (BEncodedNumber) p.ConnectionUri.Port},
                    {"peer id", p.PeerId}
                };
                list.Add (dict);
            }

            VerifyDecodedPeers (PeerDecoder.Decode (list, AddressFamily.InterNetwork));
        }

        [Test]
        public void DecodePeerId ()
        {
            var peerId = new BEncodedString (Enumerable.Repeat ((byte) 255, 20).ToArray ());
            var dict = new BEncodedDictionary {
                {"ip", (BEncodedString) "127.1.2.3"},
                {"port", (BEncodedNumber) 12345}
            };
            dict["peer id"] = peerId;

            var peer = PeerDecoder.Decode (new BEncodedList { dict }, AddressFamily.InterNetwork).Single ();
            Assert.AreEqual (peerId, peer.PeerId, "#1");
        }


        [Test]
        public void DecodeDictionaryIPv6_WithCorruptEntry ()
        {
            List<PeerInfo> actual = new List<PeerInfo> {
                new PeerInfo (new Uri ("ipv6://[::1]:1234")),
                new PeerInfo (new Uri ("ipv6://[::1234:2345:3456]:5451")),
                new PeerInfo (new Uri ("ipv6://[fe80::08ef]:5555")),
            };

            var list = new BEncodedList ();
            foreach (PeerInfo p in actual) {
                list.Add (new BEncodedDictionary {
                    {"ip", (BEncodedString) p.ConnectionUri.Host},
                    {"port", (BEncodedNumber) p.ConnectionUri.Port},
                    {"peer id", p.PeerId}
                });
            }

            // Add a corrupt port and ip
            list.Add (new BEncodedDictionary {
                {"ip", (BEncodedString) "fake"},
                {"port", (BEncodedNumber) 1234},
                {"peer id", (BEncodedString) "bad ip"}
            });

            list.Add (new BEncodedDictionary {
                {"ip", (BEncodedString) "::1"},
                {"port", (BEncodedNumber) 12345678},
                {"peer id", (BEncodedString) "bad port"}
            });

            // non-compact responses do not need the AddressFamily hint, and ipv6 address will parse correctly regardless of the value of the hint flag.
            VerifyDecodedPeers (PeerDecoder.Decode (list, AddressFamily.InterNetwork), actual);
            VerifyDecodedPeers (PeerDecoder.Decode (list, AddressFamily.InterNetworkV6), actual);
        }


        void VerifyDecodedPeers (IList<PeerInfo> decoded)
        {
            Assert.AreEqual (peers.Count, decoded.Count, "#1");
            foreach (PeerInfo dec in decoded)
                Assert.IsTrue (peers.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }

        static void VerifyDecodedPeers (IList<PeerInfo> decoded, List<PeerInfo> actual)
        {
            Assert.AreEqual (actual.Count, decoded.Count, "#1");
            foreach (PeerInfo dec in decoded)
                Assert.IsTrue (actual.Exists (p => p.ConnectionUri.Equals (dec.ConnectionUri)));
        }
    }
}
