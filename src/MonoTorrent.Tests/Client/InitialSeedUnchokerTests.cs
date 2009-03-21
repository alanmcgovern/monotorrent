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

namespace MonoTorrent.Tests.Client
{
    [TestFixture]
    public class InitialSeedUnchokerTests
    {
        static void Main()
        {
            InitialSeedUnchokerTests t = new InitialSeedUnchokerTests();
            t.FixtureSetup();
            t.Setup();
            t.Unchoke();
        }
        PeerId peer;
        TestRig rig;
        InitialSeedUnchoker unchoker;
        
        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateSingleFile();
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            peer = new PeerId(new Peer(new string('a', 20), new Uri("tcp://127.0.0.5:5353")), rig.Manager);
            peer.ProcessingQueue = true;
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
            List<PeerId> peers = new List<PeerId>(new PeerId[] { rig.CreatePeer(), rig.CreatePeer(), rig.CreatePeer()});
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
        public void ConnectDisconnect()
        {
            PeerId a = new PeerId(new Peer(new string('a', 20), new Uri("tcp://127.0.0.5:5353")), rig.Manager);
            PeerId b = new PeerId(new Peer(new string('b', 20), new Uri("tcp://127.0.0.5:5354")), rig.Manager);
            PeerId c = new PeerId(new Peer(new string('c', 20), new Uri("tcp://127.0.0.5:5355")), rig.Manager);
            PeerId d = new PeerId(new Peer(new string('d', 20), new Uri("tcp://127.0.0.5:5356")), rig.Manager);

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
        public void Unchoke()
        {
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0) Assert.IsInstanceOf<HaveMessage>(peer.Dequeue(), "#1");
            peer.IsInterested = true;
            unchoker.UnchokeReview();
            Assert.AreEqual(1, peer.QueueLength);
            Assert.IsInstanceOf<UnchokeMessage>(peer.Dequeue(), "#2");
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
            Assert.IsInstanceOf<UnchokeMessage>(peer.Dequeue(), "#2");

            while (pieces.Count > 0)
            {
                unchoker.ReceivedHave(peer, pieces.Dequeue());
                unchoker.UnchokeReview();
                Assert.AreEqual(1, peer.QueueLength);
                Assert.IsInstanceOf<HaveMessage>(peer.Dequeue(), "#3");
            }
        }
    }
}
