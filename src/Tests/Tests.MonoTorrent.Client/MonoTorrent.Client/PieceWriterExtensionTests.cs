
using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class PieceWriterExtensionTests
    {
        [Test]
        public void Offset_FirstFile ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024);
            Assert.AreEqual (0, files.FindFileByOffset (0));
            Assert.AreEqual (0, files.FindFileByOffset (1023));
            Assert.AreEqual (1, files.FindFileByOffset (1024));
        }

        [Test]
        public void Offset_LastFile ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize);
            Assert.AreEqual (files.Length - 1, files.FindFileByOffset (Constants.BlockSize));
            Assert.AreEqual (files.Length - 1, files.FindFileByOffset (Constants.BlockSize + 1));
        }

        [Test]
        public void Offset_EmptyFileAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByOffset (Constants.BlockSize * 2 - 1));
        }

        [Test]
        public void Offset_TwoEmptyFilesAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            // If two files start at the same offset (which zero length files do), then the files are ordered based on
            // their length. This way zero length files are never the last file, unless the whole torrent is empty. Which is nonsense :p
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize, 0, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByOffset (Constants.BlockSize * 2 - 1));
        }

        [Test]
        public void Offset_TwoPiecesHaveEmptyFilesAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, 0, Constants.BlockSize, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByOffset (Constants.BlockSize));
            Assert.AreEqual (0, files.FindFileByOffset (0));
        }

        [Test]
        public void Offset_Invalid ()
        {
            var files = TorrentFileInfo.Create (Constants.BlockSize, 1024);
            Assert.Less (files.FindFileByOffset (Constants.BlockSize * 5), 0);
        }

        [Test]
        public void PieceIndex_FirstFile ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024);
            Assert.AreEqual (0, files.FindFileByPieceIndex (0));
        }

        [Test]
        public void PieceIndex_LastFile ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize);
            Assert.AreEqual (files.Length - 1, files.FindFileByPieceIndex (1));
        }

        [Test]
        public void PieceIndex_OverlappingFiles ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, Constants.BlockSize - 1, Constants.BlockSize, Constants.BlockSize);
            Assert.AreEqual (0, files.FindFileByPieceIndex (0));
            Assert.AreEqual (1, files.FindFileByPieceIndex (1));
        }

        [Test]
        public void PieceIndex_OverlappingFiles2 ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, Constants.BlockSize + 1, Constants.BlockSize, Constants.BlockSize);
            Assert.AreEqual (0, files.FindFileByPieceIndex (0));
            Assert.AreEqual (0, files.FindFileByPieceIndex (1));
            Assert.AreEqual (1, files.FindFileByPieceIndex (2));
        }

        [Test]
        public void PieceIndex_EmptyFileAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByPieceIndex (1));
        }

        [Test]
        public void PieceIndex_TwoEmptyFilesAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, Constants.BlockSize, 0, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByPieceIndex (1));
        }

        [Test]
        public void PieceIndex_TwoPiecesHaveEmptyFilesAtEnd ()
        {
            var pieceLength = Constants.BlockSize;
            var files = TorrentFileInfo.Create (pieceLength, 1024, 1024, 1024, pieceLength - 3 * 1024, 0, Constants.BlockSize, 0);
            Assert.AreEqual (files.Length - 1, files.FindFileByPieceIndex (1));
            Assert.AreEqual (0, files.FindFileByPieceIndex (0));
        }

        [Test]
        public void PieceIndex_Invalid ()
        {
            var files = TorrentFileInfo.Create (Constants.BlockSize, 1024);
            Assert.Less (files.FindFileByPieceIndex (1), 0);
        }

    }
}
