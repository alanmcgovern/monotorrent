//
// TrackerTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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


using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.TrackerServer
{
    [TestFixture]
    public class TrackerTest
    {
        public TrackerTest ()
        {
        }
        private TrackerTestRig rig;

        [SetUp]
        public void Setup ()
        {
            rig = new TrackerTestRig ();
        }

        [TearDown]
        public void Teardown ()
        {
            rig.Dispose ();
        }

        [Test]
        public void AddTrackableTest ()
        {
            // Make sure they all add in
            AddAllTrackables ();

            // Ensure none are added a second time
            rig.Trackables.ForEach (delegate (Trackable t) { Assert.IsFalse (rig.Tracker.Add (t), "#2"); });

            // Clone each one and ensure that the clone can't be added
            List<Trackable> clones = new List<Trackable> ();
            rig.Trackables.ForEach (delegate (Trackable t) { clones.Add (new Trackable (Clone (t.InfoHash), t.Name)); });

            clones.ForEach (delegate (Trackable t) { Assert.IsFalse (rig.Tracker.Add (t), "#3"); });

            Assert.AreEqual (rig.Trackables.Count, rig.Tracker.Count, "#4");
        }

        [Test]
        public void GetManagerTest ()
        {
            AddAllTrackables ();
            rig.Trackables.ForEach (delegate (Trackable t) { Assert.IsNotNull (rig.Tracker.GetTrackerItem (t)); });
        }

        [Test]
        public void AnnouncePeersTest ()
        {
            AddAllTrackables ();
            rig.Peers.ForEach (delegate (PeerDetails d) { rig.Listener.Handle (d, TorrentEvent.Started, rig.Trackables[0]); });

            var manager = rig.Tracker.GetTrackerItem (rig.Trackables[0]);

            Assert.AreEqual (rig.Peers.Count, manager.Count, "#1");
            foreach (ITrackable t in rig.Trackables) {
                var m = rig.Tracker.GetTrackerItem (t);
                if (m == manager)
                    continue;
                Assert.AreEqual (0, m.Count, "#2");
            }

            foreach (Peer p in manager.GetPeers (AddressFamily.InterNetwork)) {
                PeerDetails d = rig.Peers.Find (details =>
                    details.ClientAddress == p.ClientAddress.Address && details.Port == p.ClientAddress.Port);
                Assert.AreEqual (d.Downloaded, p.Downloaded, "#3");
                Assert.AreEqual (d.peerId, p.PeerId, "#4");
                Assert.AreEqual (d.Remaining, p.Remaining, "#5");
                Assert.AreEqual (d.Uploaded, p.Uploaded, "#6");
            }
        }

        [Test]
        public void AnnounceInvalidTest ()
        {
            int i = 0;
            rig.Peers.ForEach (delegate (PeerDetails d) { rig.Listener.Handle (d, (TorrentEvent) ((i++) % 4), rig.Trackables[0]); });
            Assert.AreEqual (0, rig.Tracker.Count, "#1");
        }

        [Test]
        public void CheckPeersAdded ()
        {
            int i = 0;
            AddAllTrackables ();

            List<PeerDetails>[] lists = { new List<PeerDetails> (), new List<PeerDetails> (), new List<PeerDetails> (), new List<PeerDetails> () };
            rig.Peers.ForEach (delegate (PeerDetails d) {
                lists[i % 4].Add (d);
                rig.Listener.Handle (d, TorrentEvent.Started, rig.Trackables[i++ % 4]);
            });

            for (i = 0; i < 4; i++) {
                var manager = rig.Tracker.GetTrackerItem (rig.Trackables[i]);
                List<Peer> peers = manager.GetPeers (AddressFamily.InterNetwork);
                Assert.AreEqual (25, peers.Count, "#1");

                foreach (Peer p in peers) {
                    Assert.IsTrue (lists[i].Exists (d => d.Port == p.ClientAddress.Port &&
                                                         d.ClientAddress == p.ClientAddress.Address));
                }
            }
        }

        [Test]
        public void CustomKeyTest ()
        {
            rig.Tracker.Add (rig.Trackables[0], new CustomComparer ());
            rig.Listener.Handle (rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Loopback;
            rig.Listener.Handle (rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Broadcast;
            rig.Listener.Handle (rig.Peers[0], TorrentEvent.Started, rig.Trackables[0]);

            Assert.AreEqual (1, rig.Tracker.GetTrackerItem (rig.Trackables[0]).GetPeers (AddressFamily.InterNetwork).Count, "#1");
        }

        [Test]
        public void Scrape_One ()
        {
            AddAllTrackables ();

            var trackable = rig.Trackables.Last ();

            var query = $"?info_hash={trackable.InfoHash.UrlEncode ()}";
            var result = rig.Listener.Handle (query, IPAddress.Broadcast, true);
            var files = (BEncodedDictionary) result["files"];

            Assert.AreEqual (1, files.Count, "#1");
            Assert.IsTrue (files.ContainsKey (new BEncodedString (trackable.InfoHash.Span.ToArray ())), "#1");
        }

        [Test]
        public void Scrape_Ten ()
        {
            AddAllTrackables ();

            var query = string.Join ("&", rig.Trackables.Select (t => $"info_hash={t.InfoHash.UrlEncode ()}"));
            var result = rig.Listener.Handle (query, IPAddress.Broadcast, true);
            var files = (BEncodedDictionary) result["files"];
            Assert.AreEqual (rig.Trackables.Count, files.Count, "#1");
            foreach (var trackable in rig.Trackables)
                Assert.IsTrue (files.ContainsKey (new BEncodedString (trackable.InfoHash.Span.ToArray ())), "#2");
        }

        [Test]
        public void TestReturnedPeers ()
        {
            rig.Tracker.AllowNonCompact = true;
            rig.Tracker.Add (rig.Trackables[0]);

            List<PeerDetails> peers = new List<PeerDetails> ();
            for (int i = 0; i < 25; i++)
                peers.Add (rig.Peers[i]);

            for (int i = 0; i < peers.Count; i++)
                rig.Listener.Handle (peers[i], TorrentEvent.Started, rig.Trackables[0]);

            BEncodedDictionary dict = (BEncodedDictionary) rig.Listener.Handle (rig.Peers[24], TorrentEvent.None, rig.Trackables[0]);
            BEncodedList list = (BEncodedList) dict["peers"];
            Assert.AreEqual (25, list.Count, "#1");

            foreach (BEncodedDictionary d in list) {
                IPAddress up = IPAddress.Parse (d["ip"].ToString ());
                int port = (int) ((BEncodedNumber) d["port"]).Number;
                BEncodedString peerId = (BEncodedString) d["peer id"];

                Assert.IsTrue (peers.Exists (pd =>
                    pd.ClientAddress.Equals (up) && pd.Port == port && pd.peerId.Equals (peerId)), "#2");
            }
        }

        private void AddAllTrackables ()
        {
            rig.Trackables.ForEach (delegate (Trackable t) { Assert.IsTrue (rig.Tracker.Add (t), "#1"); });
        }

        private InfoHash Clone (InfoHash p)
        {
            return InfoHash.UrlDecode (p.UrlEncode ());
        }
    }
}
