using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrentTests;
using MonoTorrent.Client.Messages.Standard;

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
    }
}
