//
// TorrentInfoHelpers.cs
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
using System.Collections.Generic;
using System.Linq;

namespace MonoTorrent
{
    class TestTorrentManagerInfo : ITorrentManagerInfo
    {
        public IList<ITorrentManagerFile> Files => TorrentInfo.Files.Cast<ITorrentManagerFile> ().ToList ();

        public InfoHashes InfoHashes => TorrentInfo.InfoHashes;

        public string Name => TorrentInfo.Name;

        public TestTorrentInfo TorrentInfo { get; set; }

        ITorrentInfo ITorrentManagerInfo.TorrentInfo => TorrentInfo;

        public int TotalBlocks => (int) Math.Ceiling ((float) TorrentInfo.Size / Constants.BlockSize);

        public static TestTorrentManagerInfo Create (
            int? pieceLength = null,
            long? size = null,
            InfoHashes infoHashes = null,
            string name = null,
            IList<ITorrentFile> files = null)
        {
            return Create<TestTorrentManagerInfo> (pieceLength, size, infoHashes, name, files);
        }

        public static T Create<T> (
            int? pieceLength = null,
            long? size = null,
            InfoHashes infoHashes = null,
            string name = null,
            IList<ITorrentFile> files = null)
            where T : TestTorrentManagerInfo, new ()
        {
            return new T {
                TorrentInfo = new TestTorrentInfo {
                    Files = files ?? Array.Empty<ITorrentFile> (),
                    Name = name ?? "name",
                    PieceLength = pieceLength ?? (4 * 16 * 1024),
                    Size = size ?? (4 * 16 * 1024) * 32,
                    InfoHashes = new InfoHashes (new InfoHash (new byte[20]), null)
                }
            };
        }
    }

    class TestTorrentInfo : ITorrentInfo
    {
        public IList<ITorrentFile> Files { get; set; }
        public InfoHashes InfoHashes { get; set; }
        public string Name { get; set; }
        public int PieceLength { get; set; }
        public long Size { get; set; }
    }
}
