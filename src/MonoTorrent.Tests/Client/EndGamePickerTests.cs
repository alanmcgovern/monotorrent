using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class EndGamePickerTests
    {
        static void Main()
        {
            EndGamePickerTests t = new EndGamePickerTests();
            t.FixtureSetup();
            t.Setup();
            t.MultiPick2();
        }
        List<Block> alreadyGot;
        BitField bitfield;
        PeerId id;
        PeerId other;
        EndGamePicker picker;
        List<Piece> pieces;
        TestRig rig;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
        }

        [SetUp]
        public void Setup()
        {
            alreadyGot = new List<Block>();
            bitfield = new BitField(40).SetAll(true)
                                       .Set(4, false)
                                       .Set(6, false)
                                       .Set(24, false)
                                       .Set(36, false);
            picker = new EndGamePicker();
            pieces = new List<Piece>(new Piece[] { 
                new Piece(4, rig.Torrent.PieceLength, rig.Torrent.Size),
                new Piece(6, rig.Torrent.PieceLength, rig.Torrent.Size),
                new Piece(24, rig.Torrent.PieceLength, rig.Torrent.Size),
                new Piece(36, rig.Torrent.PieceLength, rig.Torrent.Size)
            });

            id = new PeerId(new Peer("peerid", new Uri("tcp://weburl.com")), rig.Manager);
            id.IsChoking = false;
            id.BitField.SetAll(true);

            other = new PeerId(new Peer("other", new Uri("tcp://other.com")), rig.Manager);
            other.IsChoking = false;
            other.BitField.SetAll(true);
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }


        [Test]
        public void MultiPick1()
        {
            RequestMessage m;
            other.IsChoking = false;
            other.BitField.SetAll(true);

            picker.Initialise(bitfield, rig.Torrent.Files, pieces);

            List<RequestMessage> requests = new List<RequestMessage>();
            while ((m = picker.PickPiece(other, new List<PeerId>())) != null)
                requests.Add(m);

            Assert.AreEqual(requests.Count, rig.BlocksPerPiece * pieces.Count, "#1");

            id.IsChoking = false;
            id.BitField.SetAll(true);
            List<RequestMessage> requests2 = new List<RequestMessage>();
            while ((m = picker.PickPiece(id, new List<PeerId>())) != null)
                requests2.Add(m);

            Assert.AreEqual(requests.Count, rig.BlocksPerPiece * pieces.Count, "#2");

            Assert.AreEqual(64, id.AmRequestingPiecesCount, "#3");
            Assert.AreEqual(64, id.AmRequestingPiecesCount, "#4");
        }

        [Test]
        public void MultiPick2()
        {
            pieces.RemoveRange(1, pieces.Count - 1);
            for (int i = 2; i < pieces[0].BlockCount; i++)
            {
                pieces[0].Blocks[i].Requested = true;
                pieces[0].Blocks[i].Received = true;
            }
            
            picker.Initialise(bitfield, rig.Torrent.Files, pieces);

            // Pick blocks 1 and 2 for both peers
            while (picker.PickPiece(id, new List<PeerId>()) != null) ;
            while (picker.PickPiece(other, new List<PeerId>()) != null) ;

            Assert.AreEqual(2, id.AmRequestingPiecesCount, "#1");
            Assert.AreEqual(2, other.AmRequestingPiecesCount, "#1");

            Piece piece;
            if (!picker.ValidatePiece(id, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out piece))
                Assert.Fail("I should've validated!");

            if (picker.ValidatePiece(other, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out piece))
                Assert.Fail("I should not have validated!");

            Assert.AreEqual(1, id.AmRequestingPiecesCount, "#1");
            Assert.AreEqual(1, other.AmRequestingPiecesCount, "#1");
            Assert.IsTrue(pieces[0][0].Received, "#5");
            Assert.AreEqual(16, pieces[0].TotalRequested, "#6");
            Assert.AreEqual(15, pieces[0].TotalReceived, "#7");
        }

        [Test]
        public void HashFail()
        {
            Piece piece;
            RequestMessage m;
            List<RequestMessage> requests = new List<RequestMessage>();

            id.BitField.SetAll(false).Set(0, true);
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());

            while ((m = picker.PickPiece(id, new List<PeerId>())) != null)
                requests.Add(m);

            foreach (RequestMessage message in requests)
                Assert.IsTrue(picker.ValidatePiece(id, message.PieceIndex, message.StartOffset, message.RequestLength, out piece));

            Assert.IsNotNull(picker.PickPiece(id, new List<PeerId>()));
        }
    }
}
