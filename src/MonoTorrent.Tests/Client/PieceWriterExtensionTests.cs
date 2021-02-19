using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PieceWriterExtensionTests
    {
        [Test]
        public void Offset_FirstFile ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024);
            Assert.AreEqual (0, IPieceWriterExtensions.FindFileByOffset (files, 0, pieceLength));
            Assert.AreEqual (0, IPieceWriterExtensions.FindFileByOffset (files, 1023, pieceLength));
            Assert.AreEqual (1, IPieceWriterExtensions.FindFileByOffset (files, 1024, pieceLength));
        }

        [Test]
        public void Offset_LastFile ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize);
            Assert.AreEqual (files.Length - 1, IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize, pieceLength));
            Assert.AreEqual (files.Length - 1, IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize + 1, pieceLength));
        }

        [Test]
        public void Offset_EmptyFileAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize, 0);
            Assert.AreEqual (files.Length - 1, IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize * 2 - 1, pieceLength));
        }

        [Test]
        public void Offset_TwoEmptyFilesAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            // If two files start at the same offset (which zero length files do), then the files are ordered based on
            // their length. This way zero length files are never the last file, unless the whole torrent is empty. Which is nonsense :p
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize, 0, 0);
            Assert.AreEqual (files.Length - 1, IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize * 2 - 1, pieceLength));
        }

        [Test]
        public void Offset_TwoPiecesHaveEmptyFilesAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, 0, Piece.BlockSize, 0);
            Assert.AreEqual (files.Length - 2, IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize, pieceLength));
            Assert.AreEqual (0, IPieceWriterExtensions.FindFileByOffset (files, 0, pieceLength));
        }

        [Test]
        public void Offset_Invalid ()
        {
            var files = TorrentFileInfo.Create (Piece.BlockSize, 1024);
            Assert.Less (IPieceWriterExtensions.FindFileByOffset (files, Piece.BlockSize * 5, Piece.BlockSize), 0);
        }


        [Test]
        public void PieceIndex_FirstFile ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024);
            Assert.AreEqual (0, IPieceWriterExtensions.FindFileByPieceIndex (files, 0));
        }

        [Test]
        public void PieceIndex_LastFile ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize);
            Assert.AreEqual (files.Length - 1, IPieceWriterExtensions.FindFileByPieceIndex (files, 1));
        }

        [Test]
        public void PieceIndex_EmptyFileAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize, 0);
            Assert.AreEqual (files.Length - 2, IPieceWriterExtensions.FindFileByPieceIndex (files, 1));
        }

        [Test]
        public void PieceIndex_TwoEmptyFilesAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Piece.BlockSize, 0, 0);
            Assert.AreEqual (files.Length - 3, IPieceWriterExtensions.FindFileByPieceIndex (files, 1));
        }

        [Test]
        public void PieceIndex_TwoPiecesHaveEmptyFilesAtEnd ()
        {
            var pieceLength = Piece.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, 0, Piece.BlockSize, 0);
            Assert.AreEqual (files.Length - 2, IPieceWriterExtensions.FindFileByPieceIndex (files, 1));
            Assert.AreEqual (0, IPieceWriterExtensions.FindFileByPieceIndex (files, 0));
        }

        [Test]
        public void PieceIndex_Invalid()
        {
            var files = TorrentFileInfo.Create (Piece.BlockSize, 1024);
            Assert.Less (IPieceWriterExtensions.FindFileByPieceIndex (files, 1), 0);
        }

    }
}
