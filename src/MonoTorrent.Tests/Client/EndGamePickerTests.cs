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
        //static void Main()
        //{
        //    EndGamePickerTests t = new EndGamePickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.PickRemaining();
        //}
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
            id = new PeerId(new Peer("peerid", new Uri("tcp://weburl.com")), rig.Manager);
            other = new PeerId(new Peer("other", new Uri("tcp://other.com")), rig.Manager);
        }

        [SetUp]
        public void Setup()
        {
            alreadyGot = new List<Block>();
            bitfield = new BitField(40).SetAll(true);
            picker = new EndGamePicker();
            pieces = new List<Piece>(new Piece[] { 
                new Piece(4, rig.Torrent),
                new Piece(6, rig.Torrent),
                new Piece(36, rig.Torrent),
                new Piece(24, rig.Torrent)
            });

            for (int i = 0; i < pieces.Count; i++)
            {
                for (int j = 0; j < pieces[i].BlockCount; j++)
                {
                    if (j % 3 == 0)
                    {
                        pieces[i].Blocks[j].CreateRequest(id);
                        if (j % 2 == 0)
                        {
                            pieces[i].Blocks[j].Received = true;
                        }
                        pieces[i].Blocks[j].Requested = true;
                        alreadyGot.Add(pieces[i].Blocks[j]);
                    }
                }
            }

            picker.Initialise(bitfield, rig.Torrent.Files, pieces);
        }

        [TearDown]
        public void Teardown()
        {
            
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }


        [Test]
        public void PickRemaining()
        {
            other.BitField.SetAll(true);
            other.IsChoking = false;
            List<Block> allBlocks = new List<Block>();
            pieces.ForEach(delegate(Piece p) { allBlocks.AddRange(p.Blocks); });
            allBlocks.RemoveAll(delegate(Block b){ return alreadyGot.Contains(b);});
            RequestMessage m;
            while ((m = picker.PickPiece(other, new List<PeerId>())) != null)
            {
                if(alreadyGot.Exists(delegate(Block b){
                    return b.PieceIndex == m.PieceIndex &&
                           b.StartOffset == m.StartOffset &&
                           b.RequestLength == m.RequestLength;}))
                {
                    Assert.AreEqual(0, allBlocks.Count, "#1");
                    break;
                }
                int ret = allBlocks.RemoveAll(delegate (Block b) {
                    return b.PieceIndex == m.PieceIndex &&
                           b.StartOffset == m.StartOffset &&
                           b.RequestLength == m.RequestLength;
                });
                Assert.AreEqual(1, ret, "#2");

            }
        }
    }
}
