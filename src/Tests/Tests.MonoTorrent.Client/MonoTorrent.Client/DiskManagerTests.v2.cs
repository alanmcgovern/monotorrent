//
// DiskManagerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MonoTorrent.PieceWriter;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class DiskManagerTestsV2
    {
        class ZeroWriter : IPieceWriter
        {
            public int OpenFiles => 0;
            public int MaximumOpenFiles { get; }

            public ReusableTask CloseAsync (ITorrentManagerFile file)
                => ReusableTask.CompletedTask;

            public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
            {
                throw new NotImplementedException ();
            }

            public void Dispose ()
            {
            }

            public ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
                => ReusableTask.FromResult (true);

            public ReusableTask FlushAsync (ITorrentManagerFile file)
                => ReusableTask.CompletedTask;

            public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
                => ReusableTask.CompletedTask;

            public ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
            {
                buffer.Span.Clear ();
                return ReusableTask.FromResult (buffer.Length);
            }

            public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
            {
                throw new NotImplementedException ();
            }

            public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
                => ReusableTask.CompletedTask;

            public ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
                => ReusableTask.CompletedTask;
        }

        class Info : ITorrentManagerInfo
        {
            public Info (Torrent torrent)
            {
                TorrentInfo = torrent;
                Files = torrent.Files.Select (t => new TorrentFileInfo (t, t.Path)).ToArray ();
            }

            public IList<ITorrentManagerFile> Files { get; }
            public InfoHashes InfoHashes => TorrentInfo.InfoHashes;
            public string Name => TorrentInfo.Name;
            public ITorrentInfo TorrentInfo { get; }
        }

        static string BaseDir => Path.GetDirectoryName (typeof (DiskManagerTestsV2).Assembly.Location);

        public static int[] PieceLengths => new[] {
            64,
            128,
            256,
            512
        };
        [Test]
        public async Task HashCheckV2Torrent_64 ([ValueSource("PieceLengths")] int pieceLength)
        {
            var torrent = Torrent.Load (Path.Combine (BaseDir, $"test_torrent_{pieceLength}.torrent"));
            Assert.AreEqual (pieceLength * 1024, torrent.PieceLength);

            var info = new Info (torrent);
            var manager = new DiskManager (EngineHelpers.CreateSettings (), EngineHelpers.Factories.WithPieceWriterCreator (t => new ZeroWriter ()));
            for (int i = 0; i < info.TorrentInfo.PieceCount (); i ++) {
                PieceHash dest = new PieceHash (Memory<byte>.Empty, new byte[32]);
                await manager.GetHashAsync (info, i, dest);
                Assert.IsTrue (torrent.CreatePieceHashes ().IsValid (dest, i));
            }
        }
    }
}
