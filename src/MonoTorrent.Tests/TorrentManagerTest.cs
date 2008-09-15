using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;
using System.Threading;

namespace MonoTorrent.Client.Tests
{

    [TestFixture]
    public class TorrentManagerTest
    {
        TestRig rig;
        ConnectionPair conn;

        [SetUp]
        public void Setup()
        {
            rig = new TestRig("", new TestWriter());
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
            bundle.Messages.Add(new HandshakeMessage(rig.Manager.Torrent.InfoHash, "11112222333344445555", VersionInfo.ProtocolStringV100));
            bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
            byte[] data = bundle.Encode();

            // Add the 'incoming' connection to the engine and send our payload
            rig.Listener.Add(rig.Manager, conn.Incoming);
            conn.Outgoing.EndSend(conn.Outgoing.BeginSend(data, 0, data.Length, null, null));

            try { conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(data, 0, data.Length, null, null)); }
            catch { }

            System.Threading.Thread.Sleep(100);
            Assert.IsFalse(conn.Incoming.Connected, "#1");
            Assert.IsFalse(conn.Outgoing.Connected, "#2");
        }

        [Test]
        public void UnregisteredAnnounce()
        {
            rig.Engine.Unregister(rig.Manager);
            rig.Tracker.AddPeer(new Peer("", new Uri("tcp://myCustomTcpSocket")));
            rig.Tracker.AddFailedPeer(new Peer("", new Uri("tcp://myCustomTcpSocket")));
        }

        [Test]
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
            TestRig rig2 = new TestRig("", new TestWriter());
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

        [Test]
        public void StopTest()
        {
            ManualResetEvent h = new ManualResetEvent(false);

            rig.Manager.TorrentStateChanged += delegate(object o, TorrentStateChangedEventArgs e)
            {
                if (e.OldState == TorrentState.Hashing)
                    h.Set();
            };

            rig.Manager.Start();
            h.WaitOne();
            Assert.IsTrue(rig.Manager.Stop().WaitOne(15000, false));
        }

        [Test]
        public void NoAnnouncesTest()
        {
            rig.TorrentDict.Remove("announce-list");
            Torrent t = Torrent.Load(rig.TorrentDict);
            rig.Engine.Unregister(rig.Manager);
            TorrentManager manager = new TorrentManager(t, "", new TorrentSettings());
            rig.Engine.Register(manager);
            manager.Start();
            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(manager.Stop().WaitOne(10000, true), "#1");
            Assert.IsTrue(manager.TrackerManager.Announce().WaitOne(10000, true), "#2"); ;
        }
    }
}
