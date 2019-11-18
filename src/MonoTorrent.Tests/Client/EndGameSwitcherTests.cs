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
            SmallTorrent = new TestTorrentData {
                Files = new[] { new TorrentFile("foo", length: Piece.BlockSize * 2 * 3, startIndex: 0, endIndex: 2) },
                PieceLength = Piece.BlockSize * 2,
                Size = Piece.BlockSize * 2 * 3,

                Bitfield = new BitField(3),
                Seeder = PeerId.CreateNull(3, seeder: true)
            };

            LargeTorrent = new TestTorrentData {

                Files = new[] { new TorrentFile("foo", length: Piece.BlockSize * 2 * 300, startIndex: 0, endIndex: 299) },
                PieceLength = Piece.BlockSize * 2,
                Size = Piece.BlockSize * 2 * 300,

                Bitfield = new BitField(300),
                Seeder = PeerId.CreateNull(300, seeder: true)
            };

            LargeTorrent.Seeder.IsChoking = SmallTorrent.Seeder.IsChoking = false;
            LargeTorrent.Seeder.AmInterested = SmallTorrent.Seeder.AmInterested = true;

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
    }
}
