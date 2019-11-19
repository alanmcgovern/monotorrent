using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client.PiecePicking;
using NUnit.Framework;

namespace MonoTorrent.Client.PiecePicking
{
    class EndGameSwitcherTests
    {
        class TestTorrentData : ITorrentData
        {
            public BitField Bitfield { get; set; }
            public PeerId Seeder { get; set; }

            public TorrentFile[] Files { get; set; }
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        TestTorrentData LargeTorrent;
        TestTorrentData SmallTorrent;

        EndGamePicker Endgame;
        StandardPicker Standard;
        EndGameSwitcher Switcher;

        [SetUp]
        public void Setup ()
        {
            // Three pieces of length 32kb.
            SmallTorrent = new TestTorrentData {
                Files = new[] { new TorrentFile("foo", length: Piece.BlockSize * 2 * 3, startIndex: 0, endIndex: 2) },
                PieceLength = Piece.BlockSize * 2,
                Size = Piece.BlockSize * 2 * 3,

                Bitfield = new BitField(3),
                Seeder = PeerId.CreateNull(3, seeder: true, isChoking: false, amInterested: true)
            };

            // Three hundred pieces of length 4MB.
            LargeTorrent = new TestTorrentData {
                Files = new[] { new TorrentFile("foo", length: Piece.BlockSize * 2 * 300, startIndex: 0, endIndex: 299) },
                PieceLength = Piece.BlockSize * 256 ,
                Size = Piece.BlockSize * 256 * 300,

                Bitfield = new BitField(300),
                Seeder = PeerId.CreateNull(300, seeder: true, isChoking: false, amInterested: true)
            };

            Standard = new StandardPicker();
            Endgame = new EndGamePicker();
            Switcher = new EndGameSwitcher(Standard, Endgame, null);
        }

        [Test]
        public void SmallTorrent_InitializeSwitcher_NoExisting ()
        {
            Switcher.Initialise (SmallTorrent.Bitfield, SmallTorrent, Enumerable.Empty<Piece> ());
            Assert.AreEqual (0, Endgame.ExportActiveRequests().Count, "#1");
            Assert.AreEqual (0, Standard.ExportActiveRequests().Count, "#2");
            Assert.AreSame(Standard, Switcher.ActivePicker, "#3");

            Assert.IsNotNull (Standard.PickPiece (SmallTorrent.Seeder, SmallTorrent.Seeder.BitField, Array.Empty<IPieceRequester>()), "#4");
            Assert.IsNotNull (Endgame.PickPiece  (SmallTorrent.Seeder, SmallTorrent.Seeder.BitField, Array.Empty<IPieceRequester>()), "#5");
        }

        [Test]
        public void SmallTorrent_InitializeSwitcher_WithExisting ()
        {
            var piece = new Piece (0, SmallTorrent.PieceLength, SmallTorrent.Size);
            piece.Blocks[0].CreateRequest (SmallTorrent.Seeder);

            Switcher.Initialise (SmallTorrent.Bitfield, SmallTorrent, new[] { piece });
            Assert.AreEqual (1, Standard.ExportActiveRequests().Count, "#1");
            Assert.AreEqual (0, Endgame.ExportActiveRequests().Count, "#2");
            Assert.AreSame (Standard, Switcher.ActivePicker, "#3");
        }

        [Test]
        public void SmallTorrent_RequestAll_TriggerEndgame ()
        {
            Switcher.Initialise (SmallTorrent.Bitfield, SmallTorrent, Enumerable.Empty<Piece> ());
            // Pretend we have all the pieces except one.
            SmallTorrent.Bitfield.SetAll (true).Set (0, false);
            // When picking we should indicate there's 1 piece that we desire - the one we're missing.
            var onePieceLeft = SmallTorrent.Bitfield.Clone ().Not ();

            // Only 2 blocks should be left to be requested, so add 1 request per person.
            var seeders = new [] {
                PeerId.CreateNull (SmallTorrent.Bitfield.Length, seeder: true, isChoking: false, amInterested: true),
                PeerId.CreateNull (SmallTorrent.Bitfield.Length, seeder: true, isChoking: false, amInterested: true),
            };

            foreach (var peer in seeders) {
                Assert.IsNotNull (Switcher.PickPiece (peer, onePieceLeft, Array.Empty<IPieceRequester> ()), "#1");
                Assert.AreEqual (1, peer.AmRequestingPiecesCount, "#2");
                Assert.AreSame (Standard, Switcher.ActivePicker, "#3");
            }

            // The next request *should* trigger endgame mode and give a valid request.
            Assert.IsNotNull (Switcher.PickPiece (SmallTorrent.Seeder, onePieceLeft, Array.Empty<IPieceRequester> ()), "#4");
            Assert.AreSame (Endgame, Switcher.ActivePicker, "#5");
        }

        [Test]
        public void LargeTorrent_RequestAll_TriggerEndgame ()
        {
            Switcher.Initialise (LargeTorrent.Bitfield, LargeTorrent, Enumerable.Empty<Piece> ());
            // Pretend we have all the pieces except one.
            LargeTorrent.Bitfield.SetAll (true).Set (0, false);
            // When picking we should indicate there's 1 piece that we desire - the one we're missing.
            var onePieceLeft = LargeTorrent.Bitfield.Clone ().Not ();

            // Only 2 blocks should be left to be requested, so add 1 request per person.
            var seeders = new List<PeerId>();
            for (int i = 0; i < LargeTorrent.PieceLength / Piece.BlockSize; i ++)
                seeders.Add (PeerId.CreateNull (LargeTorrent.Bitfield.Length, seeder: true, isChoking: false, amInterested: true));

            // 256 blocks per piece, request 1 block per peer.
            foreach (var peer in seeders) {
                Assert.IsNotNull (Switcher.PickPiece (peer, onePieceLeft, Array.Empty<IPieceRequester> ()), "#1");
                Assert.AreEqual (1, peer.AmRequestingPiecesCount, "#2");
                Assert.AreSame (Standard, Switcher.ActivePicker, "#3");
            }

            // The final request *should* trigger endgame mode and give a valid request.
            Assert.IsNotNull (Switcher.PickPiece (LargeTorrent.Seeder, onePieceLeft, Array.Empty<IPieceRequester> ()), "#4");
            Assert.AreSame (Endgame, Switcher.ActivePicker, "#5");
        }
    }
}
