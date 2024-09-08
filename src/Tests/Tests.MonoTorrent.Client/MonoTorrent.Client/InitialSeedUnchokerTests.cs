//
// InitialSeedUnchokerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Collections.Generic;
using System.Threading.Tasks;

using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Messages.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class InitialSeedUnchokerTests
    {
        PeerId peer;
        TestRig rig;
        InitialSeedUnchoker unchoker;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            rig = TestRig.CreateMultiFile ();
        }

        [OneTimeTearDown]
        public void Teardown ()
        {
            rig.Dispose ();
        }

        [SetUp]
        public async Task Setup ()
        {
            rig.Manager.UploadingTo = 0;
            await rig.Manager.UpdateSettingsAsync (new TorrentSettingsBuilder (rig.Manager.Settings) { UploadSlots = 4 }.ToSettings ());
            peer = rig.CreatePeer (true);
            unchoker = new InitialSeedUnchoker (rig.Manager);
            unchoker.PeerConnected (peer);
        }

        [Test]
        public void Advertise ()
        {
            Assert.IsTrue (!peer.IsInterested, "#1");
            Assert.IsTrue (peer.AmChoking, "#2");
            unchoker.UnchokeReview ();
            Assert.IsTrue (!peer.IsInterested, "#3");
            Assert.IsTrue (peer.AmChoking, "#4");
        }

        [Test]
        public void Advertise2 ()
        {
            unchoker.UnchokeReview ();
            Assert.AreEqual (unchoker.MaxAdvertised, peer.MessageQueue.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual (i, ((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex, "#3." + i);
        }

        [Test]
        public void Advertise3 ()
        {
            peer.MutableBitField.SetTrue (1).SetTrue (3).SetTrue (5).SetTrue (7);

            unchoker.UnchokeReview ();
            Assert.AreEqual (unchoker.MaxAdvertised, peer.MessageQueue.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual (i * 2, ((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex, "#3." + i);
        }

        [Test]
        public void Advertise4 ()
        {
            unchoker.UnchokeReview ();
            while (peer.MessageQueue.QueueLength > 0)
                peer.MessageQueue.TryDequeue ();
            unchoker.UnchokeReview ();
            Assert.AreEqual (0, peer.MessageQueue.QueueLength, "#4");
        }

        [Test]
        public void Advertise5 ()
        {
            List<PeerId> peers = new List<PeerId> (new[] { rig.CreatePeer (true), rig.CreatePeer (true), rig.CreatePeer (true) });
            peers.ForEach (unchoker.PeerConnected);
            peers.Add (this.peer);

            peers[0].MutableBitField.SetTrue (0).SetTrue (7).SetTrue (14);
            peers[1].MutableBitField.SetTrue (2).SetTrue (6).SetTrue (10);
            peers[2].MutableBitField.SetTrue (5).SetTrue (9).SetTrue (12);

            unchoker.UnchokeReview ();

            foreach (PeerId peer in peers) {
                while (peer.MessageQueue.QueueLength > 0) {
                    int index = ((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex;
                    Assert.IsFalse (peers.Exists (p => p.BitField[index]));
                }
            }
        }

        [Test]
        public void Advertise6 ()
        {
            unchoker.UnchokeReview ();
            Assert.AreEqual (unchoker.MaxAdvertised, peer.MessageQueue.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual (i, ((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex, "#3." + i);
            peer.MutableBitField.SetTrue ((0, 10));
            unchoker.UnchokeReview ();
            Assert.AreEqual (unchoker.MaxAdvertised, peer.MessageQueue.QueueLength, "#4");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual (i + 11, ((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex, "#5." + i);
        }

        [Test]
        public async Task Advertise7 ()
        {
            PeerId other = rig.CreatePeer (true);
            // Check that peers which don't share only get a small number of pieces to share
            await rig.Manager.UpdateSettingsAsync (new TorrentSettingsBuilder (rig.Manager.Settings) { UploadSlots = 1 }.ToSettings ());
            unchoker.PeerDisconnected (peer);
            List<PeerId> peers = new List<PeerId> (new[] { peer, rig.CreatePeer (true) });
            peers.ForEach (unchoker.PeerConnected);
            unchoker.UnchokeReview ();

            peers.ForEach (delegate (PeerId id) { id.IsInterested = true; });
            unchoker.UnchokeReview ();

            Assert.AreEqual (unchoker.MaxAdvertised + 1, peers[0].MessageQueue.QueueLength);
            while (peers[0].MessageQueue.QueueLength > 1)
                unchoker.ReceivedHave (peers[0], ((HaveMessage) peers[0].MessageQueue.TryDequeue ()).PieceIndex);
            unchoker.UnchokeReview ();
            Assert.IsInstanceOf (typeof (UnchokeMessage), peers[0].MessageQueue.TryDequeue ());
            Assert.IsInstanceOf (typeof (ChokeMessage), peers[0].MessageQueue.TryDequeue ());

            Assert.AreEqual (unchoker.MaxAdvertised + 1, peers[1].MessageQueue.QueueLength);
            while (peers[1].MessageQueue.QueueLength > 1)
                unchoker.ReceivedHave (other, ((HaveMessage) peers[1].MessageQueue.TryDequeue ()).PieceIndex);
            unchoker.UnchokeReview ();
            Assert.IsInstanceOf (typeof (UnchokeMessage), peers[1].MessageQueue.TryDequeue ());
            Assert.IsInstanceOf (typeof (ChokeMessage), peers[1].MessageQueue.TryDequeue ());

            // He didn't share any, he should get 1 piece.
            Assert.AreEqual (1 + 1, peers[0].MessageQueue.QueueLength);
            while (peers[0].MessageQueue.QueueLength > 1)
                unchoker.ReceivedHave (peers[0], ((HaveMessage) peers[0].MessageQueue.TryDequeue ()).PieceIndex);
            unchoker.UnchokeReview ();
            Assert.IsInstanceOf (typeof (UnchokeMessage), peers[0].MessageQueue.TryDequeue ());
            Assert.IsInstanceOf (typeof (ChokeMessage), peers[0].MessageQueue.TryDequeue ());

            // He shared them all, he should get max allowance
            Assert.AreEqual (unchoker.MaxAdvertised + 1, peers[1].MessageQueue.QueueLength);
            while (peers[1].MessageQueue.QueueLength > 1)
                unchoker.ReceivedHave (other, ((HaveMessage) peers[1].MessageQueue.TryDequeue ()).PieceIndex);
            unchoker.UnchokeReview ();
            Assert.IsInstanceOf (typeof (UnchokeMessage), peers[1].MessageQueue.TryDequeue ());
            Assert.IsInstanceOf (typeof (ChokeMessage), peers[1].MessageQueue.TryDequeue ());
        }

        [Test]
        public void Choke ()
        {
            PeerId other = rig.CreatePeer (true);
            // More slots than peers
            for (int i = 0; i < 25; i++) {
                unchoker.UnchokeReview ();
                Assert.AreEqual (unchoker.MaxAdvertised, peer.MessageQueue.QueueLength, "#1." + i);
                HaveMessage h = (HaveMessage) peer.MessageQueue.TryDequeue ();
                Assert.AreEqual (i, h.PieceIndex, "#2." + i);
                unchoker.ReceivedHave (peer, h.PieceIndex);
                unchoker.ReceivedHave (other, h.PieceIndex);
            }
        }

        [Test]
        public async Task Choke2 ()
        {
            PeerId other = rig.CreatePeer (true);

            // More peers than slots
            unchoker.PeerDisconnected (this.peer);
            await rig.Manager.UpdateSettingsAsync (new TorrentSettingsBuilder (rig.Manager.Settings) { UploadSlots = 1 }.ToSettings ());

            List<PeerId> peers = new List<PeerId> (new[] { this.peer, rig.CreatePeer (true), rig.CreatePeer (true) });
            peers.ForEach (unchoker.PeerConnected);

            unchoker.UnchokeReview ();
            peers.ForEach (delegate (PeerId p) { p.IsInterested = true; });
            unchoker.UnchokeReview ();
            Assert.IsFalse (peers[0].AmChoking);
            Assert.IsTrue (peers[1].AmChoking);
            Assert.IsTrue (peers[2].AmChoking);

            for (int current = 0; current < peers.Count; current++) {
                PeerId peer = peers[current];
                Assert.IsFalse (peer.AmChoking);
                Queue<int> haves = new Queue<int> ();

                for (int i = 0; i < unchoker.MaxAdvertised; i++)
                    haves.Enqueue (((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex);
                Assert.IsInstanceOf (typeof (UnchokeMessage), peer.MessageQueue.TryDequeue ());

                while (haves.Count > 0) {
                    unchoker.UnchokeReview ();
                    Assert.IsFalse (peer.AmChoking);
                    peers.ForEach (delegate (PeerId p) { if (p != peer) Assert.IsTrue (p.AmChoking); });
                    Assert.AreEqual (0, peer.MessageQueue.QueueLength);
                    unchoker.ReceivedHave (other, haves.Dequeue ());
                }

                unchoker.UnchokeReview ();
                Assert.IsTrue (peer.AmChoking);
                Assert.IsInstanceOf (typeof (ChokeMessage), peer.MessageQueue.TryDequeue ());
            }

            Assert.IsFalse (peers[0].AmChoking);
            Assert.IsTrue (peers[1].AmChoking);
            Assert.IsTrue (peers[2].AmChoking);

            peers.ForEach (delegate (PeerId p) { Assert.Less (0, p.MessageQueue.QueueLength); });
        }

        [Test]
        public void ConnectDisconnect ()
        {
            var peers = new[] {
                new Peer (new PeerInfo (new Uri ("ipv4://127.0.0.5:5353"), new string ('a', 20))),
                new Peer (new PeerInfo (new Uri ("ipv4://127.0.0.5:5354"), new string ('b', 20))),
                new Peer (new PeerInfo (new Uri ("ipv4://127.0.0.5:5355"), new string ('c', 20))),
                new Peer (new PeerInfo (new Uri ("ipv4://127.0.0.5:5356"), new string ('d', 20)))
            };
            PeerId a = new PeerId (peers[0], NullConnection.Incoming, new BitField (rig.Manager.Torrent.PieceCount ()), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
            PeerId b = new PeerId (peers[1], NullConnection.Incoming, new BitField (rig.Manager.Torrent.PieceCount ()), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
            PeerId c = new PeerId (peers[2], NullConnection.Incoming, new BitField (rig.Manager.Torrent.PieceCount ()), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);
            PeerId d = new PeerId (peers[3], NullConnection.Incoming, new BitField (rig.Manager.Torrent.PieceCount ()), rig.Manager.InfoHashes.V1OrV2, PlainTextEncryption.Instance, PlainTextEncryption.Instance, Software.Synthetic);

            unchoker.PeerDisconnected (a);
            Assert.AreEqual (1, unchoker.PeerCount, "#1");

            unchoker.PeerDisconnected (b);
            unchoker.PeerDisconnected (c);
            Assert.AreEqual (1, unchoker.PeerCount, "#2");

            unchoker.PeerConnected (a);
            Assert.AreEqual (2, unchoker.PeerCount, "#3");

            unchoker.PeerConnected (b);
            Assert.AreEqual (3, unchoker.PeerCount, "#4");

            unchoker.PeerDisconnected (d);
            Assert.AreEqual (3, unchoker.PeerCount, "#5");

            unchoker.PeerDisconnected (b);
            Assert.AreEqual (2, unchoker.PeerCount, "#6");
        }

        [Test]
        public void SeedConnects ()
        {
            unchoker.PeerConnected (PeerId.CreateNull (rig.Manager.Bitfield.Length, seeder: false, false, false, rig.Manager.InfoHashes.V1OrV2));
            Assert.IsFalse (unchoker.Complete);

            unchoker.PeerConnected (PeerId.CreateNull (rig.Manager.Bitfield.Length, seeder: true, false, false, rig.Manager.InfoHashes.V1OrV2));
            Assert.IsTrue (unchoker.Complete);
        }

        [Test]
        public void Unchoke ()
        {
            unchoker.UnchokeReview ();
            while (peer.MessageQueue.QueueLength > 0)
                Assert.IsInstanceOf (typeof (HaveMessage), peer.MessageQueue.TryDequeue (), "#1");
            peer.IsInterested = true;
            unchoker.UnchokeReview ();
            Assert.AreEqual (1, peer.MessageQueue.QueueLength);
            Assert.IsInstanceOf (typeof (UnchokeMessage), peer.MessageQueue.TryDequeue (), "#2");
            unchoker.UnchokeReview ();
            unchoker.UnchokeReview ();
            Assert.AreEqual (0, peer.MessageQueue.QueueLength);
        }

        [Test]
        public void Unchoke2 ()
        {
            Queue<int> pieces = new Queue<int> ();
            unchoker.UnchokeReview ();
            while (peer.MessageQueue.QueueLength > 0)
                pieces.Enqueue (((HaveMessage) peer.MessageQueue.TryDequeue ()).PieceIndex);

            peer.IsInterested = true;
            unchoker.UnchokeReview ();
            Assert.AreEqual (1, peer.MessageQueue.QueueLength);
            Assert.IsInstanceOf (typeof (UnchokeMessage), peer.MessageQueue.TryDequeue (), "#2");

            while (pieces.Count > 0) {
                unchoker.ReceivedHave (peer, pieces.Dequeue ());
                unchoker.UnchokeReview ();
                Assert.AreEqual (1, peer.MessageQueue.QueueLength);
                Assert.IsInstanceOf (typeof (HaveMessage), peer.MessageQueue.TryDequeue (), "#3");
            }
        }
    }
}
