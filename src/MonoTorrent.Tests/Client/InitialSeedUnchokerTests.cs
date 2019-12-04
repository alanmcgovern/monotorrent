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
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class InitialSeedUnchokerTests
    {
        PeerId peer;
        TestRig rig;
        InitialSeedUnchoker unchoker;
        
        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            rig.Manager.UploadingTo = 0;
            rig.Manager.Settings.UploadSlots = 4;
            peer = rig.CreatePeer(true);
            unchoker = new InitialSeedUnchoker(rig.Manager);
            unchoker.PeerConnected(peer);
        }

        [Test]
        public void Advertise()
        {
            Assert.IsTrue(!peer.IsInterested, "#1");
            Assert.IsTrue(peer.AmChoking, "#2");
            unchoker.UnchokeReview();
            Assert.IsTrue(!peer.IsInterested, "#3");
            Assert.IsTrue(peer.AmChoking, "#4");
        }

        [Test]
        public void Advertise2()
        {
            unchoker.UnchokeReview();
            Assert.AreEqual(unchoker.MaxAdvertised, peer.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual(i, ((HaveMessage)peer.Dequeue()).PieceIndex, "#3." + i);
        }

        [Test]
        public void Advertise3()
        {
            peer.BitField.SetTrue(1).SetTrue(3).SetTrue(5).SetTrue(7);

            unchoker.UnchokeReview();
            Assert.AreEqual(unchoker.MaxAdvertised, peer.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual(i * 2, ((HaveMessage)peer.Dequeue()).PieceIndex, "#3." + i);
        }

        [Test]
        public void Advertise4()
        {
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0)
                peer.Dequeue();
            unchoker.UnchokeReview();
            Assert.AreEqual(0, peer.QueueLength, "#4");
        }

        [Test]
        public void Advertise5()
        {
            List<PeerId> peers = new List<PeerId>(new PeerId[] { rig.CreatePeer(true), rig.CreatePeer(true), rig.CreatePeer(true) });
            peers.ForEach(unchoker.PeerConnected);
            peers.Add(this.peer);

            peers[0].BitField.SetTrue(0).SetTrue(7).SetTrue(14);
            peers[1].BitField.SetTrue(2).SetTrue(6).SetTrue(10);
            peers[2].BitField.SetTrue(5).SetTrue(9).SetTrue(12);

            unchoker.UnchokeReview();

            foreach (PeerId peer in peers)
            {
                while (peer.QueueLength > 0)
                {
                    int index = ((HaveMessage)peer.Dequeue()).PieceIndex;
                    Assert.IsFalse(peers.Exists(delegate(PeerId p) { return p.BitField[index]; }));
                }
            }
        }

        [Test]
        public void Advertise6()
        {
            unchoker.UnchokeReview();
            Assert.AreEqual(unchoker.MaxAdvertised, peer.QueueLength, "#2");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual(i, ((HaveMessage)peer.Dequeue()).PieceIndex, "#3." + i);
            peer.BitField.SetTrue(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            unchoker.UnchokeReview();
            Assert.AreEqual(unchoker.MaxAdvertised, peer.QueueLength, "#4");
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.AreEqual(i + 11, ((HaveMessage)peer.Dequeue()).PieceIndex, "#5." + i);
        }

        [Test]
        public void Advertise7()
        {
            PeerId other = rig.CreatePeer(true);
            // Check that peers which don't share only get a small number of pieces to share
            rig.Manager.Settings.UploadSlots = 1;
            unchoker.PeerDisconnected(peer);
            List<PeerId> peers = new List<PeerId>(new PeerId[] { peer, rig.CreatePeer(true) });
            peers.ForEach(unchoker.PeerConnected);
            unchoker.UnchokeReview();

            peers.ForEach(delegate(PeerId id) { id.IsInterested = true; });
            unchoker.UnchokeReview();

            Assert.AreEqual(unchoker.MaxAdvertised + 1, peers[0].QueueLength);
            while (peers[0].QueueLength > 1)
                unchoker.ReceivedHave(peers[0], ((HaveMessage)peers[0].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsInstanceOf(typeof (UnchokeMessage), peers[0].Dequeue());
            Assert.IsInstanceOf(typeof (ChokeMessage), peers[0].Dequeue());

            Assert.AreEqual(unchoker.MaxAdvertised + 1, peers[1].QueueLength);
            while (peers[1].QueueLength > 1)
                unchoker.ReceivedHave(other, ((HaveMessage)peers[1].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsInstanceOf(typeof (UnchokeMessage), peers[1].Dequeue());
            Assert.IsInstanceOf(typeof (ChokeMessage), peers[1].Dequeue());

            // He didn't share any, he should get 1 piece.
            Assert.AreEqual(1 + 1, peers[0].QueueLength);
            while (peers[0].QueueLength > 1)
                unchoker.ReceivedHave(peers[0], ((HaveMessage)peers[0].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsInstanceOf(typeof (UnchokeMessage), peers[0].Dequeue());
            Assert.IsInstanceOf(typeof (ChokeMessage), peers[0].Dequeue());

            // He shared them all, he should get max allowance
            Assert.AreEqual(unchoker.MaxAdvertised + 1, peers[1].QueueLength);
            while (peers[1].QueueLength > 1)
                unchoker.ReceivedHave(other, ((HaveMessage)peers[1].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsInstanceOf(typeof (UnchokeMessage), peers[1].Dequeue());
            Assert.IsInstanceOf(typeof (ChokeMessage), peers[1].Dequeue());
        }

        [Test]
        public void Choke()
        {
            PeerId other = rig.CreatePeer(true);
            // More slots than peers
            for (int i = 0; i < 25; i++)
            {
                unchoker.UnchokeReview();
                Assert.AreEqual(unchoker.MaxAdvertised, peer.QueueLength, "#1." + i);
                HaveMessage h = (HaveMessage)peer.Dequeue();
                Assert.AreEqual(i, h.PieceIndex, "#2." + i);
                unchoker.ReceivedHave(peer, h.PieceIndex);
                unchoker.ReceivedHave(other, h.PieceIndex);
            }
        }

        [Test]
        public void Choke2()
        {
            PeerId other = rig.CreatePeer(true);

            // More peers than slots
            unchoker.PeerDisconnected(this.peer);
            rig.Manager.Settings.UploadSlots = 1;

            List<PeerId> peers = new List<PeerId>(new PeerId[] { this.peer, rig.CreatePeer(true), rig.CreatePeer(true) });
            peers.ForEach(unchoker.PeerConnected);

            unchoker.UnchokeReview();
            peers.ForEach(delegate(PeerId p) { p.IsInterested = true; });
            unchoker.UnchokeReview();
            Assert.IsFalse(peers[0].AmChoking);
            Assert.IsTrue(peers[1].AmChoking);
            Assert.IsTrue(peers[2].AmChoking);

            for (int current = 0; current < peers.Count; current++)
            {
                PeerId peer = peers[current];
                Assert.IsFalse(peer.AmChoking);
                Queue<int> haves = new Queue<int>();

                for (int i = 0; i < unchoker.MaxAdvertised; i++)
                    haves.Enqueue(((HaveMessage)peer.Dequeue()).PieceIndex);
                Assert.IsInstanceOf(typeof (UnchokeMessage), peer.Dequeue());

                while(haves.Count > 0)
                {
                    unchoker.UnchokeReview();
                    Assert.IsFalse(peer.AmChoking);
                    peers.ForEach(delegate(PeerId p) { if (p != peer) Assert.IsTrue(p.AmChoking); });
                    Assert.AreEqual(0, peer.QueueLength);
                    unchoker.ReceivedHave(other, haves.Dequeue());
                }

                unchoker.UnchokeReview();
                Assert.IsTrue(peer.AmChoking);
                Assert.IsInstanceOf(typeof (ChokeMessage), peer.Dequeue());
            }

            Assert.IsFalse(peers[0].AmChoking);
            Assert.IsTrue(peers[1].AmChoking);
            Assert.IsTrue(peers[2].AmChoking);

            peers.ForEach(delegate(PeerId p) { Assert.Less(0, p.QueueLength); });
        }

        [Test]
        public void ConnectDisconnect()
        {
            PeerId a = new PeerId(new Peer(new string('a', 20), new Uri("ipv4://127.0.0.5:5353")), NullConnection.Incoming, rig.Manager.Bitfield?.Clone ().SetAll (false));
            PeerId b = new PeerId(new Peer(new string('b', 20), new Uri("ipv4://127.0.0.5:5354")), NullConnection.Incoming, rig.Manager.Bitfield?.Clone ().SetAll (false));
            PeerId c = new PeerId(new Peer(new string('c', 20), new Uri("ipv4://127.0.0.5:5355")), NullConnection.Incoming, rig.Manager.Bitfield?.Clone ().SetAll (false));
            PeerId d = new PeerId(new Peer(new string('d', 20), new Uri("ipv4://127.0.0.5:5356")), NullConnection.Incoming, rig.Manager.Bitfield?.Clone ().SetAll (false));

            unchoker.PeerDisconnected(a);
            Assert.AreEqual(1, unchoker.PeerCount, "#1");
            
            unchoker.PeerDisconnected(b);
            unchoker.PeerDisconnected(c);
            Assert.AreEqual(1, unchoker.PeerCount, "#2");
            
            unchoker.PeerConnected(a);
            Assert.AreEqual(2, unchoker.PeerCount, "#3");
            
            unchoker.PeerConnected(b);
            Assert.AreEqual(3, unchoker.PeerCount, "#4");

            unchoker.PeerDisconnected(d);
            Assert.AreEqual(3, unchoker.PeerCount, "#5");

            unchoker.PeerDisconnected(b);
            Assert.AreEqual(2, unchoker.PeerCount, "#6");
        }

        [Test]
        public void SeedConnects ()
        {
            unchoker.PeerConnected (PeerId.CreateNull (rig.Manager.Bitfield.Length, seeder: false, false, false));
            Assert.IsFalse (unchoker.Complete);

            unchoker.PeerConnected (PeerId.CreateNull (rig.Manager.Bitfield.Length, seeder: true, false, false));
            Assert.IsTrue (unchoker.Complete);
        }

        [Test]
        public void Unchoke()
        {
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0) Assert.IsInstanceOf(typeof (HaveMessage), peer.Dequeue(), "#1");
            peer.IsInterested = true;
            unchoker.UnchokeReview();
            Assert.AreEqual(1, peer.QueueLength);
            Assert.IsInstanceOf(typeof (UnchokeMessage), peer.Dequeue(), "#2");
            unchoker.UnchokeReview();
            unchoker.UnchokeReview();
            Assert.AreEqual(0, peer.QueueLength);
        }

        [Test]
        public void Unchoke2()
        {
            Queue<int> pieces = new Queue<int>();
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0)
                pieces.Enqueue(((HaveMessage)peer.Dequeue()).PieceIndex);

            peer.IsInterested = true;
            unchoker.UnchokeReview();
            Assert.AreEqual(1, peer.QueueLength);
            Assert.IsInstanceOf(typeof (UnchokeMessage), peer.Dequeue(), "#2");

            while (pieces.Count > 0)
            {
                unchoker.ReceivedHave(peer, pieces.Dequeue());
                unchoker.UnchokeReview();
                Assert.AreEqual(1, peer.QueueLength);
                Assert.IsInstanceOf(typeof (HaveMessage), peer.Dequeue(), "#3");
            }
        }
    }
}
