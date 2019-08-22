//
// TorrentManagerTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

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
        public async Task AddConnectionToStoppedManager()
        {
            MessageBundle bundle = new MessageBundle();

            // Create the handshake and bitfield message
            bundle.Messages.Add(new HandshakeMessage(rig.Manager.InfoHash, "11112222333344445555", VersionInfo.ProtocolStringV100));
            bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
            byte[] data = bundle.Encode();

            // Add the 'incoming' connection to the engine and send our payload
            rig.Listener.Add(rig.Manager, conn.Incoming);
            await conn.Outgoing.SendAsync (data, 0, data.Length);

            try {
                var received = await conn.Outgoing.ReceiveAsync(data, 0, data.Length);
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
        public async Task ReregisterManager()
        {
            var hashingTask = rig.Manager.WaitForState(TorrentState.Stopped);
            await rig.Manager.HashCheckAsync(false);
            await hashingTask;

            await rig.Engine.Unregister(rig.Manager);
            TestRig rig2 = TestRig.CreateMultiFile (new TestWriter());
            await rig2.Engine.Unregister(rig2.Manager);
            await rig.Engine.Register(rig2.Manager);

            hashingTask = rig2.Manager.WaitForState(TorrentState.Downloading);
            await rig2.Manager.HashCheckAsync(true);
            await hashingTask;
            await rig2.Manager.StopAsync();

            rig2.Dispose();
        }

        [Test]
        public async Task StopTest()
        {
            var hashingState = rig.Manager.WaitForState(TorrentState.Hashing);
            var stoppedState = rig.Manager.WaitForState(TorrentState.Stopped);

            await rig.Manager.StartAsync();
            Assert.IsTrue(hashingState.Wait(5000), "Started");
            await rig.Manager.StopAsync();
            Assert.IsTrue(stoppedState.Wait(5000), "Stopped");
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
            foreach (MonoTorrent.Client.Tracker.TrackerTier t in manager.TrackerManager.Tiers)
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
            rig.Manager.TrackerManager.AnnounceComplete += delegate {
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
