using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrentTests;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.FastPeer;

namespace MonoTorrent.Tests
{
    [TestFixture]
    public class PiecePickerTests
    {
        static void Main(string[] args)
        {
            PiecePickerTests t = new PiecePickerTests();
            t.Setup();
            t.RequestFastSeeder();
            t.Setup();
            t.RequestFastNotSeeder();
            t.Setup();
            t.RequestFastHaveEverything();
            t.Setup();
            t.RequestWhenSeeder();
            t.Setup();
            t.NoInterestingPieces();
            t.Setup();
            t.CancelRequests();
            t.Setup();
            t.RejectRequests();
            t.Setup();
            t.PeerChoked();
            t.Setup();
            t.FastPeerChoked();
            t.Setup();
            t.ChokeThenClose();
        }
        PeerIdInternal peer;
        List<PeerIdInternal> peers;
        StandardPicker picker;
        TestRig rig;


        [SetUp]
        public void Setup()
        {
            // Yes, this is horrible. Deal with it.
            rig = new TestRig("");
            peers = new List<PeerIdInternal>();
            picker = new StandardPicker(rig.Manager.Bitfield, rig.Torrent.Files);
            peer = new PeerIdInternal(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            peer.Connection = new PeerConnectionBase(rig.Manager.Bitfield.Length);
            for (int i = 0; i < 20; i++)
            {
                PeerIdInternal p = new PeerIdInternal(new Peer(new string(i.ToString()[0], 20), new Uri("tcp://" + i)), rig.Manager);
                p.Connection = new PeerConnectionBase(rig.Manager.Bitfield.Length);
                p.Connection.SupportsFastPeer = true;
                peers.Add(p);
            }
        }

        [Test]
        public void RequestFastSeeder()
        {
            peers[0].Connection.SupportsFastPeer = true;
            peers[0].Connection.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].Connection.BitField.SetAll(true); // Lets pretend he has everything
            for (int i = 0; i < 7; i++)
                for (int j = 0; j < 16; j++)
                    Assert.IsNotNull(picker.PickPiece(peers[0], peers));

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }
        [Test]
        public void RequestFastNotSeeder()
        {
            peers[0].Connection.SupportsFastPeer = true;
            peers[0].Connection.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].Connection.BitField.SetAll(true);
            peers[0].Connection.BitField[1] = false;
            peers[0].Connection.BitField[3] = false;
            peers[0].Connection.BitField[5] = false;

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 16; j++)
                {
                    RequestMessage m = picker.PickPiece(peers[0], peers);
                    Assert.IsTrue(m.PieceIndex == 2 || m.PieceIndex == 8 || m.PieceIndex == 13 || m.PieceIndex == 21);
                } 

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }
        [Test]
        public void RequestFastHaveEverything()
        {
            peers[0].Connection.SupportsFastPeer = true;
            peers[0].Connection.IsAllowedFastPieces.AddRange(new int[] { 1, 2, 3, 5, 8, 13, 21 });

            peers[0].Connection.BitField.SetAll(true);
            picker.MyBitField.SetAll(true);

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void RequestChoked()
        {
            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void RequestWhenSeeder()
        {
            picker.MyBitField.SetAll(true);
            peers[0].Connection.BitField.SetAll(true);
            peers[0].Connection.IsChoking = false;

            Assert.IsNull(picker.PickPiece(peers[0], peers));
        }

        [Test]
        public void NoInterestingPieces()
        {
            peer.Connection.IsChoking = false;
            for (int i = 0; i < picker.MyBitField.Length; i++)
                if (i % 2 == 0)
                {
                    peer.Connection.BitField[i] = true;
                    picker.MyBitField[i] = true;
                }
            Assert.IsNull(picker.PickPiece(peer, peers));
        }

        [Test]
        public void CancelRequests()
        {
            List<RequestMessage> messages = new List<RequestMessage>();
            peer.Connection.IsChoking = false;
            peer.Connection.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.RemoveRequests(peer);

            List<RequestMessage> messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]));
        }

        [Test]
        public void RejectRequests()
        {
            List<RequestMessage> messages = new List<RequestMessage>();
            peer.Connection.IsChoking = false;
            peer.Connection.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            foreach (RequestMessage message in messages)
                picker.ReceivedRejectRequest(peer, new RejectRequestMessage(message.PieceIndex, message.StartOffset, message.RequestLength));

            List<RequestMessage> messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }

        [Test]
        public void PeerChoked()
        {
            List<RequestMessage> messages = new List<RequestMessage>();
            peer.Connection.IsChoking = false;
            peer.Connection.BitField.SetAll(true);

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.ReceivedChokeMessage(peer);

            List<RequestMessage> messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }

        [Test]
        public void FastPeerChoked()
        {
            List<RequestMessage> messages = new List<RequestMessage>();
            peer.Connection.IsChoking = false;
            peer.Connection.BitField.SetAll(true);
            peer.Connection.SupportsFastPeer = true;

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.ReceivedChokeMessage(peer);

            List<RequestMessage> messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(0, messages2.Count, "#1");
        }

        [Test]
        public void ChokeThenClose()
        {
            List<RequestMessage> messages = new List<RequestMessage>();
            peer.Connection.IsChoking = false;
            peer.Connection.BitField.SetAll(true);
            peer.Connection.SupportsFastPeer = true;

            RequestMessage m;
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages.Add(m);

            picker.ReceivedChokeMessage(peer);
            picker.RemoveRequests(peer);

            List<RequestMessage> messages2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(peer, peers)) != null)
                messages2.Add(m);

            Assert.AreEqual(messages.Count, messages2.Count, "#1");
            for (int i = 0; i < messages.Count; i++)
                Assert.IsTrue(messages2.Contains(messages[i]), "#2." + i);
        }
    }
}
