//
// TorrentTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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


using System;
using System.IO;
using System.Linq;
using System.Text;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentV2Test
    {
        [SetUp]
        public void Setup ()
        {
            Torrent.SupportsV2Torrents = true;
        }

        [TearDown]
        public void Teardown ()
        {
            Torrent.SupportsV2Torrents = false;
        }

        [Test]
        public void LoadSingleFile()
        {
            var torrent = Torrent.Load (Encoding.UTF8.GetBytes ("d4:infod9:file treed4:dir1d4:dir2d9:fileA.txtd0:d6:lengthi1024e11:pieces root32:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaeeeee12:piece lengthi32768eee"));
            var file = torrent.Files.Single ();
            Assert.AreEqual (Path.Combine ("dir1", "dir2", "fileA.txt"), file.Path);
            Assert.AreEqual (0, file.StartPieceIndex);
            Assert.AreEqual (0, file.EndPieceIndex);
            Assert.AreEqual (0, file.OffsetInTorrent);

            var hash = Enumerable.Repeat ((byte) 'a', 32).ToArray ().AsMemory ();
            Assert.IsTrue (hash.Span.SequenceEqual (file.PiecesRoot.Span));
        }

        [Test]
        public void LoadingMetadataVersion2FailsBEP52Unsupported ()
        {
            Torrent.SupportsV2Torrents = false;

            var dict = (BEncodedDictionary) BEncodedValue.Decode (Encoding.UTF8.GetBytes ("d4:infod9:file treed4:dir1d4:dir2d9:fileA.txtd0:d6:lengthi1024e11:pieces root32:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaeeeee12:meta versioni2e12:piece lengthi16382eee"));
            Assert.Throws<TorrentException> (() => Torrent.Load (dict));
        }

        [Test]
        public void LoadingFileTreesFailsWhenBEP52Unsupported ()
        {
            Torrent.SupportsV2Torrents = false;

            var dict = (BEncodedDictionary) BEncodedValue.Decode (Encoding.UTF8.GetBytes ("d4:infod9:file treed4:dir1d4:dir2d9:fileA.txtd0:d6:lengthi1024e11:pieces root32:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaeeeee12:piece lengthi16382eee"));
            Assert.Throws<TorrentException> (() => Torrent.Load (dict));
        }
    }
}
