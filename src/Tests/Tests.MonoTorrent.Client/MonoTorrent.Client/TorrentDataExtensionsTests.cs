//
// TorrentDataExtensionsTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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


using System.Collections.Generic;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class TorrentDataExtensionsTests
    {
        class Data : ITorrentData
        {
            public IList<ITorrentFileInfo> Files { get; set; }
            public InfoHash InfoHash => new InfoHash (new byte[20]);
            public string Name => "Test Torrent";
            public int PieceLength { get; set; }
            public long Size { get; set; }
        }

        [Test]
        public void BlocksPerPiece ()
        {
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 2, PieceLength = Constants.BlockSize * 2 }.BlocksPerPiece (0));
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.BlocksPerPiece (1));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4 + 1, PieceLength = Constants.BlockSize * 2 }.BlocksPerPiece (2));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 5 - 1, PieceLength = Constants.BlockSize * 2 }.BlocksPerPiece (2));

            Assert.AreEqual (2, new Data { Size = (long) (int.MaxValue) * 4, PieceLength = Constants.BlockSize * 2 }.BlocksPerPiece (0));

            Assert.AreEqual (142, new Data { Size = 16 * 1024 * 1024, PieceLength = 2318336 }.BlocksPerPiece (0));
        }

        [Test]
        public void ByteOffsetToPieceIndex ()
        {
            Assert.AreEqual (0, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (0));
            Assert.AreEqual (0, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (1));
            Assert.AreEqual (0, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 2 - 1));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 2));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 2 + 1));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 3 - 1));
            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 3));
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 4));

            Assert.AreEqual (2, new Data { Size = (long) (int.MaxValue) * 4, PieceLength = Constants.BlockSize * 2 }.ByteOffsetToPieceIndex (Constants.BlockSize * 4));
        }

        [Test]
        public void BytesPerPiece ()
        {
            Assert.AreEqual (Constants.BlockSize * 2, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.BytesPerPiece (0));
            Assert.AreEqual (Constants.BlockSize * 2, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.BytesPerPiece (1));

            Assert.AreEqual (1, new Data { Size = Constants.BlockSize * 4 + 1, PieceLength = Constants.BlockSize * 2 }.BytesPerPiece (2));
            Assert.AreEqual (Constants.BlockSize - 1, new Data { Size = Constants.BlockSize * 5 - 1, PieceLength = Constants.BlockSize * 2 }.BytesPerPiece (2));

            Assert.AreEqual (Constants.BlockSize * 2, new Data { Size = (long) (int.MaxValue) * 4, PieceLength = Constants.BlockSize * 2 }.BytesPerPiece (2));
        }

        [Test]
        public void PieceCount ()
        {
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 2 + 1, PieceLength = Constants.BlockSize * 2 }.PieceCount ());
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 4 - 1, PieceLength = Constants.BlockSize * 2 }.PieceCount ());
            Assert.AreEqual (2, new Data { Size = Constants.BlockSize * 4, PieceLength = Constants.BlockSize * 2 }.PieceCount ());
            Assert.AreEqual (3, new Data { Size = Constants.BlockSize * 4 + 1, PieceLength = Constants.BlockSize * 2 }.PieceCount ());

            Assert.AreEqual (262144, new Data { Size = (long) (int.MaxValue) * 4, PieceLength = Constants.BlockSize * 2 }.PieceCount ());
        }
    }
}
