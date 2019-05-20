using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;
using System.Threading;
using MonoTorrent.BEncoding;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{

    [TestFixture]
    public class TorrentManagerTest
    {
        TestRig rig;
        ConnectionPair conn;

        [SetUp]
        public void Setup()
        {
            rig = TestRig.CreateMultiFile (new TestWriter());
            conn = new ConnectionPair(51515);
        }
        [TearDown]
        public void Teardown()
        {
            rig.Dispose();
            conn.Dispose();
        }

        [Test]
        public void AddConnectionToStoppedManager()
        {
            MessageBundle bundle = new MessageBundle();

            // Create the handshake and bitfield message
            bundle.Messages.Add(new HandshakeMessage(rig.Manager.InfoHash, "11112222333344445555", VersionInfo.ProtocolStringV100));
            bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
            byte[] data = bundle.Encode();

            // Add the 'incoming' connection to the engine and send our payload
            rig.Listener.Add(rig.Manager, conn.Incoming);
            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(data, 0, data.Length, null, null));

            try {
                var received = conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(data, 0, data.Length, null, null));
                Assert.AreEqual (received, 0);
            } catch {
                Assert.IsFalse(conn.Incoming.Connected, "#1");
            }
        }

        [Test]
        public void AddPeers_PrivateTorrent ()
        {
            // You can't manually add peers to private torrents
            var dict = (BEncodedDictionary)rig.TorrentDict["info"];
            dict["private"] = (BEncodedString)"1";
            Torrent t = Torrent.Load(rig.TorrentDict);
            TorrentManager manager = new TorrentManager(t, "path", new TorrentSettings());

            Assert.ThrowsAsync<InvalidOperationException>(() => manager.AddPeerAsync(new Peer("id", new Uri("tcp:://whatever.com"))));
        }

        [Test]
        public async Task UnregisteredAnnounce()
        {
            await rig.Engine.Unregister(rig.Manager);
            rig.Tracker.AddPeer(new Peer("", new Uri("ipv4://myCustomTcpSocket")));
            Assert.AreEqual(0, rig.Manager.Peers.Available, "#1");
            rig.Tracker.AddFailedPeer(new Peer("", new Uri("ipv4://myCustomTcpSocket")));
            Assert.AreEqual(0, rig.Manager.Peers.Available, "#2");
        }

        [Test]
        public async Task ReregisterManager()
        {
            var hashingTask = rig.Manager.WaitForState(TorrentState.Stopped);
            await rig.Manager.HashCheckAsync(false);
            await hashingTask;

            await rig.Engine.Unregister(rig.Manager);
            TestRig rig2 = TestRig.CreateMultiFile (new TestWriter());
            await rig2.Engine.Unregister(rig2.Manager);
            await rig.Engine.Register(rig2.Manager);

            hashingTask = rig2.Manager.WaitForState(TorrentState.Stopped);
            await rig2.Manager.HashCheckAsync(true);
            await hashingTask;

            rig2.Dispose();
        }

        [Test]
        public async Task StopTest()
        {
            bool started = false;
            AutoResetEvent h = new AutoResetEvent(false);

            rig.Manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e)
            {
                if (!started) {
                    if ((started = e.NewState == TorrentState.Hashing))
                        h.Set();
                } else {
                    if (e.NewState == TorrentState.Stopped)
                        h.Set();
                }
            };

            await rig.Manager.StartAsync();
            Assert.IsTrue (h.WaitOne(5000, true), "Started");
            await rig.Manager.StopAsync();
            Assert.IsTrue (h.WaitOne(5000, true), "Stopped");
        }

        [Test]
        public async Task NoAnnouncesTest()
        {
            rig.TorrentDict.Remove("announce-list");
            rig.TorrentDict.Remove("announce");
            Torrent t = Torrent.Load(rig.TorrentDict);
            await rig.Engine.Unregister(rig.Manager);
            TorrentManager manager = new TorrentManager(t, "", new TorrentSettings());
            await rig.Engine.Register(manager);

            AutoResetEvent handle = new AutoResetEvent(false);
            manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e) {
                if (e.NewState == TorrentState.Downloading || e.NewState == TorrentState.Stopped)
                    handle.Set();
            };
            await manager.StartAsync();
            handle.WaitOne();
            System.Threading.Thread.Sleep(1000);
            await manager.StopAsync();

            Assert.IsTrue(handle.WaitOne(10000, true), "#1");
            await manager.TrackerManager.Announce();
        }

        [Test]
        public void UnsupportedTrackers ()
        {
            RawTrackerTier tier = new RawTrackerTier {
                "fake://123.123.123.2:5665"
            };
            rig.Torrent.AnnounceUrls.Add (tier);
            TorrentManager manager = new TorrentManager (rig.Torrent, "", new TorrentSettings());
            foreach (MonoTorrent.Client.Tracker.TrackerTier t in manager.TrackerManager)
            {
                Assert.IsTrue (t.Trackers.Count > 0, "#1");
            }
        }

        [Test]
        public async Task AnnounceWhenComplete()
        {
            // Check that the engine announces when the download starts, completes
            // and is stopped.
            AutoResetEvent handle = new AutoResetEvent(false);
            rig.Manager.TrackerManager.CurrentTracker.AnnounceComplete += delegate {
                handle.Set ();
            };

            var downloadingState = rig.Manager.WaitForState (TorrentState.Downloading);

            await rig.Manager.StartAsync();
            await downloadingState;

            Assert.IsTrue (handle.WaitOne(5000, false), "Announce on startup");
            Assert.AreEqual(1, rig.Tracker.AnnouncedAt.Count, "#2");
			Console.WriteLine ("Got start announce. State: {0}. Complete: {1}", rig.Manager.State, rig.Manager.Complete);

            rig.Manager.Bitfield.SetAll(true);
            Assert.IsTrue (handle.WaitOne (5000, false), "Announce when download completes");
            Assert.AreEqual(TorrentState.Seeding, rig.Manager.State, "#3");
            Assert.AreEqual(2, rig.Tracker.AnnouncedAt.Count, "#4");

            await rig.Manager.StopAsync();
            Assert.IsTrue (handle.WaitOne (5000, false), "Announce when torrent stops");
            Assert.AreEqual(3, rig.Tracker.AnnouncedAt.Count, "#6");
        }

        [Test]
        public async Task InvalidFastResume_NoneExist()
        {
            var handle = new ManualResetEvent (false);
            var bf = new BitField (rig.Pieces).Not ();
            rig.Manager.LoadFastResume (new FastResume (rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) => {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set ();
            };
            await rig.Manager.StartAsync ();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Test]
        public async Task InvalidFastResume_SomeExist()
        {
            rig.Writer.FilesThatExist.AddRange(new[]{
                rig.Manager.Torrent.Files [0],
                rig.Manager.Torrent.Files [2],
            });
            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            await rig.Manager.StartAsync();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Test]
        public async Task HashTorrent_ReadZero()
        {
            rig.Writer.FilesThatExist.AddRange(new[]{
                rig.Manager.Torrent.Files [0],
                rig.Manager.Torrent.Files [2],
            });
            rig.Writer.DoNotReadFrom.AddRange(new[]{
                rig.Manager.Torrent.Files[0],
                rig.Manager.Torrent.Files[3],
            });

            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            await rig.Manager.StartAsync();
            Assert.IsTrue(handle.WaitOne(), "#1");
            Assert.IsTrue(rig.Manager.Bitfield.AllFalse, "#2");
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.IsTrue (file.BitField.AllFalse, "#3." + file.Path);
        }
    }
}
