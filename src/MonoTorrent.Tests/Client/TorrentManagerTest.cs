using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using System;
using System.Threading;
using Xunit;

namespace MonoTorrent.Client
{
    public class TorrentManagerTest : IDisposable
    {
        TestRig rig;
        ConnectionPair conn;

        public TorrentManagerTest()
        {
            rig = TestRig.CreateMultiFile(new TestWriter());
            conn = new ConnectionPair(51515);
        }

        public void Dispose()
        {
            rig.Dispose();
            conn.Dispose();
        }

        [Fact]
        public void AddConnectionToStoppedManager()
        {
            MessageBundle bundle = new MessageBundle();

            // Create the handshake and bitfield message
            bundle.Messages.Add(new HandshakeMessage(rig.Manager.InfoHash, "11112222333344445555",
                VersionInfo.ProtocolStringV100));
            bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
            byte[] data = bundle.Encode();

            // Add the 'incoming' connection to the engine and send our payload
            rig.Listener.Add(rig.Manager, conn.Incoming);
            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(data, 0, data.Length, null, null));

            try
            {
                conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(data, 0, data.Length, null, null));
            }
            catch
            {
                Assert.False(conn.Incoming.Connected);
//                Assert.False(conn.Outgoing.Connected);
                return;
            }

            Assert.True(false, "The outgoing connection should've thrown an exception");
        }

        [Fact]
        public void AddPeers_PrivateTorrent()
        {
            // You can't manually add peers to private torrents
            var dict = (BEncodedDictionary) rig.TorrentDict["info"];
            dict["private"] = (BEncodedString) "1";
            Torrent t = Torrent.Load(rig.TorrentDict);
            TorrentManager manager = new TorrentManager(t, "path", new TorrentSettings());
            Assert.Throws<InvalidOperationException>(
                () => manager.AddPeers(new Peer("id", new Uri("tcp:://whatever.com"))));
        }

        [Fact]
        public void UnregisteredAnnounce()
        {
            rig.Engine.Unregister(rig.Manager);
            rig.Tracker.AddPeer(new Peer("", new Uri("tcp://myCustomTcpSocket")));
            Assert.Equal(0, rig.Manager.Peers.Available);
            rig.Tracker.AddFailedPeer(new Peer("", new Uri("tcp://myCustomTcpSocket")));
            Assert.Equal(0, rig.Manager.Peers.Available);
        }

        [Fact]
        public void ReregisterManager()
        {
            ManualResetEvent handle = new ManualResetEvent(false);
            rig.Manager.TorrentStateChanged += delegate(object sender, TorrentStateChangedEventArgs e)
            {
                if (e.OldState == TorrentState.Hashing)
                    handle.Set();
            };
            rig.Manager.HashCheck(false);

            handle.WaitOne();
            handle.Reset();

            rig.Engine.Unregister(rig.Manager);
            TestRig rig2 = TestRig.CreateMultiFile(new TestWriter());
            rig2.Engine.Unregister(rig2.Manager);
            rig.Engine.Register(rig2.Manager);
            rig2.Manager.TorrentStateChanged += delegate(object sender, TorrentStateChangedEventArgs e)
            {
                if (e.OldState == TorrentState.Hashing)
                    handle.Set();
            };
            rig2.Manager.HashCheck(true);
            handle.WaitOne();
            rig2.Dispose();
        }

        [Fact]
        public void StopTest()
        {
            bool started = false;
            AutoResetEvent h = new AutoResetEvent(false);

            rig.Manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e)
            {
                if (!started)
                {
                    if ((started = e.NewState == TorrentState.Hashing))
                        h.Set();
                }
                else
                {
                    if (e.NewState == TorrentState.Stopped)
                        h.Set();
                }
            };

            rig.Manager.Start();
            Assert.True(h.WaitOne(5000, true), "Started");
            rig.Manager.Stop();
            Assert.True(h.WaitOne(5000, true), "Stopped");
        }

        [Fact]
        public void NoAnnouncesTest()
        {
            rig.TorrentDict.Remove("announce-list");
            rig.TorrentDict.Remove("announce");
            Torrent t = Torrent.Load(rig.TorrentDict);
            rig.Engine.Unregister(rig.Manager);
            TorrentManager manager = new TorrentManager(t, "", new TorrentSettings());
            rig.Engine.Register(manager);

            AutoResetEvent handle = new AutoResetEvent(false);
            manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e)
            {
                if (e.NewState == TorrentState.Downloading || e.NewState == TorrentState.Stopped)
                    handle.Set();
            };
            manager.Start();
            handle.WaitOne();
            System.Threading.Thread.Sleep(1000);
            manager.Stop();

            Assert.True(handle.WaitOne(10000, true));
            Assert.True(manager.TrackerManager.Announce().WaitOne(10000, true));
            ;
        }

        [Fact]
        public void UnsupportedTrackers()
        {
            RawTrackerTier tier = new RawTrackerTier
            {
                "fake://123.123.123.2:5665"
            };
            rig.Torrent.AnnounceUrls.Add(tier);
            TorrentManager manager = new TorrentManager(rig.Torrent, "", new TorrentSettings());
            foreach (MonoTorrent.Client.Tracker.TrackerTier t in manager.TrackerManager)
            {
                Assert.True(t.Trackers.Count > 0);
            }
        }

        [Fact]
        public void AnnounceWhenComplete()
        {
            // Check that the engine announces when the download starts, completes
            // and is stopped.
            AutoResetEvent handle = new AutoResetEvent(false);
            rig.Manager.TrackerManager.CurrentTracker.AnnounceComplete += delegate { handle.Set(); };

            rig.Manager.Start();
            Assert.True(handle.WaitOne(5000, false), "Announce on startup");
            Assert.Equal(1, rig.Tracker.AnnouncedAt.Count);

            rig.Manager.Bitfield.SetAll(true);
            Assert.True(handle.WaitOne(5000, false), "Announce when download completes");
            Assert.Equal(TorrentState.Seeding, rig.Manager.State);
            Assert.Equal(2, rig.Tracker.AnnouncedAt.Count);

            rig.Manager.Stop();
            Assert.True(handle.WaitOne(5000, false), "Announce when torrent stops");
            Assert.Equal(3, rig.Tracker.AnnouncedAt.Count);
        }

        [Fact]
        public void InvalidFastResume_NoneExist()
        {
            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            rig.Manager.Start();
            Assert.True(handle.WaitOne());
            Assert.True(rig.Manager.Bitfield.AllFalse);
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.True(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Fact]
        public void InvalidFastResume_SomeExist()
        {
            rig.Writer.FilesThatExist.AddRange(new[]
            {
                rig.Manager.Torrent.Files[0],
                rig.Manager.Torrent.Files[2],
            });
            var handle = new ManualResetEvent(false);
            var bf = new BitField(rig.Pieces).Not();
            rig.Manager.LoadFastResume(new FastResume(rig.Manager.InfoHash, bf));
            rig.Manager.TorrentStateChanged += (o, e) =>
            {
                if (rig.Manager.State == TorrentState.Downloading)
                    handle.Set();
            };
            rig.Manager.Start();
            Assert.True(handle.WaitOne());
            Assert.True(rig.Manager.Bitfield.AllFalse);
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.True(file.BitField.AllFalse, "#3." + file.Path);
        }

        [Fact]
        public void HashTorrent_ReadZero()
        {
            rig.Writer.FilesThatExist.AddRange(new[]
            {
                rig.Manager.Torrent.Files[0],
                rig.Manager.Torrent.Files[2],
            });
            rig.Writer.DoNotReadFrom.AddRange(new[]
            {
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
            rig.Manager.Start();
            Assert.True(handle.WaitOne());
            Assert.True(rig.Manager.Bitfield.AllFalse);
            foreach (TorrentFile file in rig.Manager.Torrent.Files)
                Assert.True(file.BitField.AllFalse, "#3." + file.Path);
        }
    }
}