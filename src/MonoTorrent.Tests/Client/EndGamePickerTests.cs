using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    
    public class EndGamePickerTests
    {
        //static void Main()
        //{
        //    EndGamePickerTests t = new EndGamePickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.CancelTest();
        //}
        BitField bitfield;
        PeerId id;
        PeerId other;
        EndGamePicker picker;
        List<Piece> pieces;
        TestRig rig;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
        }

        [SetUp]
        public void Setup()
        {
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
            id.BitField.SetAll(false);

            other = new PeerId(new Peer("other", new Uri("tcp://other.com")), rig.Manager);
            other.IsChoking = false;
            other.BitField.SetAll(false);
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [Fact]
        public void CancelTest()
        {
            foreach (Piece p in pieces)
            {
                for (int i = 0; i < p.BlockCount; i++)
                {
                    if (i % 2 == 0)
                        p.Blocks[i].CreateRequest(id);
                    else
                        p.Blocks[i].CreateRequest(other);
                }
            }

            picker.Initialise(bitfield, rig.Manager.Torrent.Files, pieces);
            picker.CancelRequests(id);
            picker.CancelRequests(other);

            id.BitField[4] = true;
            Assert.NotNull(picker.PickPiece(id, new List<PeerId>()));
        }

        [Fact]
        public void MultiPick()
        {
            id.BitField.Set(pieces[0].Index, true);
            other.BitField.Set(pieces[0].Index, true);

            for (int i = 2; i < pieces[0].BlockCount; i++)
            {
                pieces[0].Blocks[i].Requested = true;
                pieces[0].Blocks[i].Received = true;
            }
            
            picker.Initialise(bitfield, rig.Torrent.Files, pieces);

            // Pick blocks 1 and 2 for both peers
            while (picker.PickPiece(id, new List<PeerId>()) != null) ;
            while (picker.PickPiece(other, new List<PeerId>()) != null) ;

            Assert.Equal(2, id.AmRequestingPiecesCount, "#1");
            Assert.Equal(2, other.AmRequestingPiecesCount, "#1");

            Piece piece;
            if (!picker.ValidatePiece(id, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out piece))
                Assert.Fail("I should've validated!");

            if (picker.ValidatePiece(other, pieces[0].Index, pieces[0][0].StartOffset, pieces[0][0].RequestLength, out piece))
                Assert.Fail("I should not have validated!");

            Assert.Equal(1, id.AmRequestingPiecesCount, "#1");
            Assert.Equal(1, other.AmRequestingPiecesCount, "#1");
            Assert.True(pieces[0][0].Received, "#5");
            Assert.Equal(16, pieces[0].TotalRequested, "#6");
            Assert.Equal(15, pieces[0].TotalReceived, "#7");
        }

        [Fact]
        public void HashFail()
        {
            Piece piece;
            RequestMessage m;
            List<RequestMessage> requests = new List<RequestMessage>();

            id.BitField[0] = true;
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());

            while ((m = picker.PickPiece(id, new List<PeerId>())) != null)
                requests.Add(m);

            foreach (RequestMessage message in requests)
                Assert.True(picker.ValidatePiece(id, message.PieceIndex, message.StartOffset, message.RequestLength, out piece));

            Assert.NotNull(picker.PickPiece(id, new List<PeerId>()));
        }
    }
}
