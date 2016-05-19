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


using MonoTorrent.Client.Messages.Standard;
using System;
using System.Collections.Generic;
using Xunit;

namespace MonoTorrent.Client
{
    public class InitialSeedUnchokerTests : IDisposable
    {
        //static void Main()
        //{
        //    InitialSeedUnchokerTests t = new InitialSeedUnchokerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.Choke();
        //}
        PeerId peer;
        TestRig rig;
        InitialSeedUnchoker unchoker;

        public InitialSeedUnchokerTests()
        {
            rig = TestRig.CreateMultiFile();

            rig.Manager.UploadingTo = 0;
            rig.Manager.Settings.UploadSlots = 4;
            peer = rig.CreatePeer(true);
            unchoker = new InitialSeedUnchoker(rig.Manager);
            unchoker.PeerConnected(peer);
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        [Fact]
        public void Advertise()
        {
            Assert.True(!peer.IsInterested);
            Assert.True(peer.AmChoking);
            unchoker.UnchokeReview();
            Assert.True(!peer.IsInterested);
            Assert.True(peer.AmChoking);
        }

        [Fact]
        public void Advertise2()
        {
            unchoker.UnchokeReview();
            Assert.Equal(unchoker.MaxAdvertised, peer.QueueLength);
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.Equal(i, ((HaveMessage) peer.Dequeue()).PieceIndex);
        }

        [Fact]
        public void Advertise3()
        {
            peer.BitField.SetTrue(1).SetTrue(3).SetTrue(5).SetTrue(7);

            unchoker.UnchokeReview();
            Assert.Equal(unchoker.MaxAdvertised, peer.QueueLength);
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.Equal(i*2, ((HaveMessage) peer.Dequeue()).PieceIndex);
        }

        [Fact]
        public void Advertise4()
        {
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0)
                peer.Dequeue();
            unchoker.UnchokeReview();
            Assert.Equal(0, peer.QueueLength);
        }

        [Fact]
        public void Advertise5()
        {
            List<PeerId> peers =
                new List<PeerId>(new PeerId[] {rig.CreatePeer(true), rig.CreatePeer(true), rig.CreatePeer(true)});
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
                    int index = ((HaveMessage) peer.Dequeue()).PieceIndex;
                    Assert.False(peers.Exists(delegate(PeerId p) { return p.BitField[index]; }));
                }
            }
        }

        [Fact]
        public void Advertise6()
        {
            unchoker.UnchokeReview();
            Assert.Equal(unchoker.MaxAdvertised, peer.QueueLength);
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.Equal(i, ((HaveMessage) peer.Dequeue()).PieceIndex);
            peer.BitField.SetTrue(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            unchoker.UnchokeReview();
            Assert.Equal(unchoker.MaxAdvertised, peer.QueueLength);
            for (int i = 0; i < unchoker.MaxAdvertised; i++)
                Assert.Equal(i + 11, ((HaveMessage) peer.Dequeue()).PieceIndex);
        }

        [Fact]
        public void Advertise7()
        {
            PeerId other = rig.CreatePeer(true);
            // Check that peers which don't share only get a small number of pieces to share
            rig.Manager.Settings.UploadSlots = 1;
            unchoker.PeerDisconnected(peer);
            List<PeerId> peers = new List<PeerId>(new PeerId[] {peer, rig.CreatePeer(true)});
            peers.ForEach(unchoker.PeerConnected);
            unchoker.UnchokeReview();

            peers.ForEach(delegate(PeerId id) { id.IsInterested = true; });
            unchoker.UnchokeReview();

            Assert.Equal(unchoker.MaxAdvertised + 1, peers[0].QueueLength);
            while (peers[0].QueueLength > 1)
                unchoker.ReceivedHave(peers[0], ((HaveMessage) peers[0].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsType(typeof(UnchokeMessage), peers[0].Dequeue());
            Assert.IsType(typeof(ChokeMessage), peers[0].Dequeue());

            Assert.Equal(unchoker.MaxAdvertised + 1, peers[1].QueueLength);
            while (peers[1].QueueLength > 1)
                unchoker.ReceivedHave(other, ((HaveMessage) peers[1].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsType(typeof(UnchokeMessage), peers[1].Dequeue());
            Assert.IsType(typeof(ChokeMessage), peers[1].Dequeue());

            // He didn't share any, he should get 1 piece.
            Assert.Equal(1 + 1, peers[0].QueueLength);
            while (peers[0].QueueLength > 1)
                unchoker.ReceivedHave(peers[0], ((HaveMessage) peers[0].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsType(typeof(UnchokeMessage), peers[0].Dequeue());
            Assert.IsType(typeof(ChokeMessage), peers[0].Dequeue());

            // He shared them all, he should get max allowance
            Assert.Equal(unchoker.MaxAdvertised + 1, peers[1].QueueLength);
            while (peers[1].QueueLength > 1)
                unchoker.ReceivedHave(other, ((HaveMessage) peers[1].Dequeue()).PieceIndex);
            unchoker.UnchokeReview();
            Assert.IsType(typeof(UnchokeMessage), peers[1].Dequeue());
            Assert.IsType(typeof(ChokeMessage), peers[1].Dequeue());
        }

        [Fact]
        public void Choke()
        {
            PeerId other = rig.CreatePeer(true);
            // More slots than peers
            for (int i = 0; i < 25; i++)
            {
                unchoker.UnchokeReview();
                Assert.Equal(unchoker.MaxAdvertised, peer.QueueLength);
                HaveMessage h = (HaveMessage) peer.Dequeue();
                Assert.Equal(i, h.PieceIndex);
                unchoker.ReceivedHave(peer, h.PieceIndex);
                unchoker.ReceivedHave(other, h.PieceIndex);
            }
        }

        [Fact]
        public void Choke2()
        {
            PeerId other = rig.CreatePeer(true);

            // More peers than slots
            unchoker.PeerDisconnected(this.peer);
            rig.Manager.Settings.UploadSlots = 1;

            List<PeerId> peers = new List<PeerId>(new PeerId[] {this.peer, rig.CreatePeer(true), rig.CreatePeer(true)});
            peers.ForEach(unchoker.PeerConnected);

            unchoker.UnchokeReview();
            peers.ForEach(delegate(PeerId p) { p.IsInterested = true; });
            unchoker.UnchokeReview();
            Assert.False(peers[0].AmChoking);
            Assert.True(peers[1].AmChoking);
            Assert.True(peers[2].AmChoking);

            for (int current = 0; current < peers.Count; current++)
            {
                PeerId peer = peers[current];
                Assert.False(peer.AmChoking);
                Queue<int> haves = new Queue<int>();

                for (int i = 0; i < unchoker.MaxAdvertised; i++)
                    haves.Enqueue(((HaveMessage) peer.Dequeue()).PieceIndex);
                Assert.IsType(typeof(UnchokeMessage), peer.Dequeue());

                while (haves.Count > 0)
                {
                    unchoker.UnchokeReview();
                    Assert.False(peer.AmChoking);
                    peers.ForEach(delegate(PeerId p)
                    {
                        if (p != peer) Assert.True(p.AmChoking);
                    });
                    Assert.Equal(0, peer.QueueLength);
                    unchoker.ReceivedHave(other, haves.Dequeue());
                }

                unchoker.UnchokeReview();
                Assert.True(peer.AmChoking);
                Assert.IsType(typeof(ChokeMessage), peer.Dequeue());
            }

            Assert.False(peers[0].AmChoking);
            Assert.True(peers[1].AmChoking);
            Assert.True(peers[2].AmChoking);

            peers.ForEach(delegate(PeerId p) { Assert.True(p.QueueLength > 0); });
        }

        [Fact]
        public void ConnectDisconnect()
        {
            PeerId a = new PeerId(new Peer(new string('a', 20), new Uri("tcp://127.0.0.5:5353")), rig.Manager);
            PeerId b = new PeerId(new Peer(new string('b', 20), new Uri("tcp://127.0.0.5:5354")), rig.Manager);
            PeerId c = new PeerId(new Peer(new string('c', 20), new Uri("tcp://127.0.0.5:5355")), rig.Manager);
            PeerId d = new PeerId(new Peer(new string('d', 20), new Uri("tcp://127.0.0.5:5356")), rig.Manager);

            unchoker.PeerDisconnected(a);
            Assert.Equal(1, unchoker.PeerCount);

            unchoker.PeerDisconnected(b);
            unchoker.PeerDisconnected(c);
            Assert.Equal(1, unchoker.PeerCount);

            unchoker.PeerConnected(a);
            Assert.Equal(2, unchoker.PeerCount);

            unchoker.PeerConnected(b);
            Assert.Equal(3, unchoker.PeerCount);

            unchoker.PeerDisconnected(d);
            Assert.Equal(3, unchoker.PeerCount);

            unchoker.PeerDisconnected(b);
            Assert.Equal(2, unchoker.PeerCount);
        }

        [Fact]
        public void Unchoke()
        {
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0) Assert.IsType(typeof(HaveMessage), peer.Dequeue());
            peer.IsInterested = true;
            unchoker.UnchokeReview();
            Assert.Equal(1, peer.QueueLength);
            Assert.IsType(typeof(UnchokeMessage), peer.Dequeue());
            unchoker.UnchokeReview();
            unchoker.UnchokeReview();
            Assert.Equal(0, peer.QueueLength);
        }

        [Fact]
        public void Unchoke2()
        {
            Queue<int> pieces = new Queue<int>();
            unchoker.UnchokeReview();
            while (peer.QueueLength > 0)
                pieces.Enqueue(((HaveMessage) peer.Dequeue()).PieceIndex);

            peer.IsInterested = true;
            unchoker.UnchokeReview();
            Assert.Equal(1, peer.QueueLength);
            Assert.IsType(typeof(UnchokeMessage), peer.Dequeue());

            while (pieces.Count > 0)
            {
                unchoker.ReceivedHave(peer, pieces.Dequeue());
                unchoker.UnchokeReview();
                Assert.Equal(1, peer.QueueLength);
                Assert.IsType(typeof(HaveMessage), peer.Dequeue());
            }
        }
    }
}