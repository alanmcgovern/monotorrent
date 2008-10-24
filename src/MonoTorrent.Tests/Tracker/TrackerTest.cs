using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Tracker;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using System.Net;

namespace MonoTorrent.Tracker
{
    [TestFixture]
    public class TrackerTest
    {
        public TrackerTest()
        {
        }
        private TrackerTestRig rig;

        [SetUp]
        public void Setup()
        {
            rig = new TrackerTestRig();
        }

        [TearDown]
        public void Teardown()
        {
            rig.Dispose();
        }

        [Test]
        public void AddTrackableTest()
        {
            // Make sure they all add in
            AddAllTrackables();

            // Ensure none are added a second time
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.IsFalse(rig.Tracker.Add(t), "#2"); });

            // Clone each one and ensure that the clone can't be added
            List<Trackable> clones = new List<Trackable>();
            rig.Trackables.ForEach(delegate(Trackable t) { clones.Add(new Trackable(Clone(t.InfoHash), t.Name)); });

            clones.ForEach(delegate(Trackable t) { Assert.IsFalse(rig.Tracker.Add(t), "#3"); });

            Assert.AreEqual(rig.Trackables.Count, rig.Tracker.Count, "#4");
        }

        [Test]
        public void GetManagerTest()
        {
            AddAllTrackables();
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.IsNotNull(rig.Tracker.GetManager(t)); });
        }

        [Test]
        public void AnnouncePeersTest()
        {
            AddAllTrackables();
            rig.Peers.ForEach(delegate(PeerDetails d) { rig.Listener.Handle(d, MonoTorrent.Common.TorrentEvent.Started, rig.Trackables[0]); });

            SimpleTorrentManager manager = rig.Tracker.GetManager(rig.Trackables[0]);

            Assert.AreEqual(rig.Peers.Count, manager.Count, "#1");
            foreach (ITrackable t in rig.Trackables)
            {
                SimpleTorrentManager m = rig.Tracker.GetManager(t);
                if (m == manager)
                    continue;
                Assert.AreEqual(0, m.Count, "#2");
            }

            foreach (Peer p in manager.GetPeers())
            {
                PeerDetails d = rig.Peers.Find(delegate(PeerDetails details) {
                    return details.ClientAddress == p.ClientAddress.Address && details.Port == p.ClientAddress.Port;
                });
                Assert.AreEqual(d.Downloaded, p.Downloaded, "#3");
                Assert.AreEqual(d.peerId, p.PeerId, "#4");
                Assert.AreEqual(d.Remaining, p.Remaining, "#5");
                Assert.AreEqual(d.Uploaded, p.Uploaded, "#6");
            }
        }

        [Test]
        public void AnnounceInvalidTest()
        {
            int i = 0;
            rig.Peers.ForEach(delegate(PeerDetails d) { rig.Listener.Handle(d, (TorrentEvent)((i++) % 4), rig.Trackables[0]); });
            Assert.AreEqual(0, rig.Tracker.Count, "#1");
        }

        [Test]
        public void CheckPeersAdded()
        {
            int i = 0;
            AddAllTrackables();

            List<PeerDetails>[] lists = new List<PeerDetails>[] { new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>() };
            rig.Peers.ForEach(delegate(PeerDetails d) {
                lists[i % 4].Add(d);
                rig.Listener.Handle(d, TorrentEvent.Started, rig.Trackables[i++ % 4]);
            });

            for (i = 0; i < 4; i++)
            {
                SimpleTorrentManager manager = rig.Tracker.GetManager(rig.Trackables[i]);
                List<Peer> peers = manager.GetPeers();
                Assert.AreEqual(25, peers.Count, "#1");

                foreach (Peer p in peers)
                {
                    Assert.IsTrue(lists[i].Exists(delegate(PeerDetails d) {
                        return d.Port == p.ClientAddress.Port &&
                            d.ClientAddress == p.ClientAddress.Address;
                    }));
                }
            }
        }

        [Test]
        public void CustomKeyTest()
        {
            rig.Tracker.Add(rig.Trackables[0], new CustomComparer());
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Loopback;
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Broadcast;
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            Assert.AreEqual(1, rig.Tracker.GetManager(rig.Trackables[0]).GetPeers().Count, "#1");
        }

        [Test]
        public void TestReturnedPeers()
        {
            rig.Tracker.AllowNonCompact = true;
            rig.Tracker.Add(rig.Trackables[0]);

            List<PeerDetails> peers = new List<PeerDetails>();
            for (int i = 0; i < 25; i++)
                peers.Add(rig.Peers[i]);

            for (int i = 0; i < peers.Count; i++)
                rig.Listener.Handle(peers[i], TorrentEvent.Started, rig.Trackables[0]);

            BEncodedDictionary dict = (BEncodedDictionary)rig.Listener.Handle(rig.Peers[24], TorrentEvent.None, rig.Trackables[0]);
            BEncodedList list = (BEncodedList)dict["peers"];
            Assert.AreEqual(25, list.Count, "#1");

            foreach (BEncodedDictionary d in list)
            {
                IPAddress up = IPAddress.Parse(d["ip"].ToString());
                int port = (int)((BEncodedNumber)d["port"]).Number;
                string peerId = ((BEncodedString)d["peer id"]).Text;

                Assert.IsTrue(peers.Exists(delegate(PeerDetails pd) {
                    return pd.ClientAddress.Equals(up) && pd.Port == port && pd.peerId == peerId;
                }), "#2");
            }
        }

        private void AddAllTrackables()
        {
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.IsTrue(rig.Tracker.Add(t), "#1"); });
        }

        private byte[] Clone(byte[] p)
        {
            byte[] clone = new byte[p.Length];
            for (int i = 0; i < p.Length; i++)
                clone[i] = p[i];
            return clone;
        }
    }
}
