using System;
using System.Collections.Generic;
using System.Net;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tracker
{
    public class TrackerTest : IDisposable
    {
        public TrackerTest()
        {
            rig = new TrackerTestRig();
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        private readonly TrackerTestRig rig;

        private void AddAllTrackables()
        {
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.True(rig.Tracker.Add(t)); });
        }

        private InfoHash Clone(InfoHash p)
        {
            return new InfoHash((byte[]) p.Hash.Clone());
        }

        [Fact]
        public void AddTrackableTest()
        {
            // Make sure they all add in
            AddAllTrackables();

            // Ensure none are added a second time
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.False(rig.Tracker.Add(t)); });

            // Clone each one and ensure that the clone can't be added
            var clones = new List<Trackable>();
            rig.Trackables.ForEach(delegate(Trackable t) { clones.Add(new Trackable(Clone(t.InfoHash), t.Name)); });

            clones.ForEach(delegate(Trackable t) { Assert.False(rig.Tracker.Add(t)); });

            Assert.Equal(rig.Trackables.Count, rig.Tracker.Count);
        }

        [Fact]
        public void AnnounceInvalidTest()
        {
            var i = 0;
            rig.Peers.ForEach(
                delegate(PeerDetails d) { rig.Listener.Handle(d, (TorrentEvent) (i++%4), rig.Trackables[0]); });
            Assert.Equal(0, rig.Tracker.Count);
        }

        [Fact]
        public void AnnouncePeersTest()
        {
            AddAllTrackables();
            rig.Peers.ForEach(
                delegate(PeerDetails d) { rig.Listener.Handle(d, TorrentEvent.Started, rig.Trackables[0]); });

            var manager = rig.Tracker.GetManager(rig.Trackables[0]);

            Assert.Equal(rig.Peers.Count, manager.Count);
            foreach (ITrackable t in rig.Trackables)
            {
                var m = rig.Tracker.GetManager(t);
                if (m == manager)
                    continue;
                Assert.Equal(0, m.Count);
            }

            foreach (var p in manager.GetPeers())
            {
                var d =
                    rig.Peers.Find(
                        delegate(PeerDetails details)
                        {
                            return details.ClientAddress == p.ClientAddress.Address &&
                                   details.Port == p.ClientAddress.Port;
                        });
                Assert.Equal(d.Downloaded, p.Downloaded);
                Assert.Equal(d.peerId, p.PeerId);
                Assert.Equal(d.Remaining, p.Remaining);
                Assert.Equal(d.Uploaded, p.Uploaded);
            }
        }

        [Fact]
        public void CheckPeersAdded()
        {
            var i = 0;
            AddAllTrackables();

            var lists = new[]
            {new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>()};
            rig.Peers.ForEach(delegate(PeerDetails d)
            {
                lists[i%4].Add(d);
                rig.Listener.Handle(d, TorrentEvent.Started, rig.Trackables[i++%4]);
            });

            for (i = 0; i < 4; i++)
            {
                var manager = rig.Tracker.GetManager(rig.Trackables[i]);
                var peers = manager.GetPeers();
                Assert.Equal(25, peers.Count);

                foreach (var p in peers)
                {
                    Assert.True(lists[i].Exists(delegate(PeerDetails d)
                    {
                        return d.Port == p.ClientAddress.Port &&
                               d.ClientAddress == p.ClientAddress.Address;
                    }));
                }
            }
        }

        [Fact]
        public void CustomKeyTest()
        {
            rig.Tracker.Add(rig.Trackables[0], new CustomComparer());
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Loopback;
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Broadcast;
            rig.Listener.Handle(rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            Assert.Equal(1, rig.Tracker.GetManager(rig.Trackables[0]).GetPeers().Count);
        }

        [Fact]
        public void GetManagerTest()
        {
            AddAllTrackables();
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.NotNull(rig.Tracker.GetManager(t)); });
        }

        [Fact]
        public void TestReturnedPeers()
        {
            rig.Tracker.AllowNonCompact = true;
            rig.Tracker.Add(rig.Trackables[0]);

            var peers = new List<PeerDetails>();
            for (var i = 0; i < 25; i++)
                peers.Add(rig.Peers[i]);

            for (var i = 0; i < peers.Count; i++)
                rig.Listener.Handle(peers[i], TorrentEvent.Started, rig.Trackables[0]);

            var dict =
                (BEncodedDictionary) rig.Listener.Handle(rig.Peers[24], TorrentEvent.None, rig.Trackables[0]);
            var list = (BEncodedList) dict["peers"];
            Assert.Equal(25, list.Count);

            foreach (BEncodedDictionary d in list)
            {
                var up = IPAddress.Parse(d["ip"].ToString());
                var port = (int) ((BEncodedNumber) d["port"]).Number;
                var peerId = ((BEncodedString) d["peer id"]).Text;

                Assert.True(
                    peers.Exists(
                        delegate(PeerDetails pd)
                        {
                            return pd.ClientAddress.Equals(up) && pd.Port == port && pd.peerId == peerId;
                        }));
            }
        }
    }
}