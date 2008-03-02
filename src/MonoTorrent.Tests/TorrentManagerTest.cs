using System;
using System.Collections.Generic;
using System.Text;
using SampleClient;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;
using System.Threading;

namespace MonoTorrent.Client.Managers.Tests
{
    public class TestWriter : PieceWriters.PieceWriter
    {
        private enum Access
        {
            Free,
            Open
        }

        public TestWriter()
        {

        }

        private TorrentFile[] files;
        private KeyValuePair<FileManager, Access>[] access;
        public override WaitHandle CloseFileStreams(TorrentManager manager)
        {
            if (access == null) return new ManualResetEvent(true) ;
            for (int i = 0; i < access.Length; i++)
                if (access[i].Key == manager.FileManager)
                    access[i] = new KeyValuePair<FileManager, Access>(access[i].Key, Access.Free);
            return new ManualResetEvent(true);
        }

        public override void Flush(TorrentManager manager)
        {
            if (files == null)
            {
                this.files = manager.Torrent.Files;
                access = new KeyValuePair<FileManager, Access>[files.Length];
            }
        }
        public override int Read(BufferedIO data)
        {
            if (files == null)
            {
                this.files = data.Manager.FileManager.Files;
                access = new KeyValuePair<FileManager, Access>[files.Length];
            }
            GetStream(data.Manager.FileManager, data.Offset);
            return data.Count;
        }

        public override void Write(BufferedIO data)
        {
            if (files == null)
            {
                this.files = data.Manager.FileManager.Files;
                access = new KeyValuePair<FileManager, Access>[files.Length];
            }
            GetStream(data.Manager.FileManager, data.Offset);
        }

        private void GetStream(FileManager manager, long offset)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (offset > files[i].Length)
                {
                    offset -= files[i].Length;
                }
                else
                {
                    if (access[i].Key != manager && access[i].Value == Access.Open)
                        Assert.Fail("The file is still open!");

                    access[i] = new KeyValuePair<FileManager, Access>(manager, Access.Open);
                    break;
                }
            }
        }
    }
    [TestFixture]
    public class TorrentManagerTest
    {
        EngineTestRig rig;
        ConnectionPair conn;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = new EngineTestRig("", new TestWriter());
        }
        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Engine.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            rig = new EngineTestRig("", new TestWriter());
            conn = new ConnectionPair(51515);
        }
        [TearDown]
        public void Teardown()
        {
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
            rig.Manager.HashCheck(true);

            handle.WaitOne();
            handle.Reset();

            rig.Engine.Unregister(rig.Manager);
            EngineTestRig rig2 = new EngineTestRig("", new TestWriter());
            rig2.Engine.Unregister(rig2.Manager);
            rig.Engine.Register(rig2.Manager);
            rig2.Manager.TorrentStateChanged += delegate(object sender, TorrentStateChangedEventArgs e)
            {
                if (e.OldState == TorrentState.Hashing)
                    handle.Set();
            };
            rig2.Manager.HashCheck(true);
            handle.WaitOne();
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
