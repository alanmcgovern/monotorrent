//
// DiskWriterTests.cs
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


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.PieceWriter
{
    static class TestLoop
    {
        public static MainLoop Current { get; } = new MainLoop ("test loop");
    }

    public class DiskWriterTests
    {
        string Temp { get; set; }
        ITorrentManagerFile[] Others { get; set; }
        ITorrentManagerFile TorrentFile { get; set; }

        [SetUp]
        public void Setup ()
        {
            var pieceLength = Constants.BlockSize * 2;
            Temp = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName () + $"-{Process.GetCurrentProcess ().Id}-monotorrent_tests");

            var files = TorrentFileInfo.Create (pieceLength,
                ("test1.file", 12345, Path.Combine (Temp, "test1.file")),
                ("test2.file", 12345, Path.Combine (Temp, "test2.file")),
                ("test3.file", 12345, Path.Combine (Temp, "test3.file")),
                ("test4.file", 12345, Path.Combine (Temp, "test4.file"))
            );

            TorrentFile = files.First ();
            Others = files.Skip (1).ToArray ();
        }

        [TearDown]
        public void Teardown ()
        {
            if (Directory.Exists (Temp))
                Directory.Delete (Temp, true);
        }

        [Test]
        public async Task CloseFileAsync_Opened ()
        {
            await TestLoop.Current;
            using var writer = new DiskWriter ();

            await writer.WriteAsync (TorrentFile, 0, new byte[10]);
            Assert.IsTrue (File.Exists (TorrentFile.FullPath));

            Assert.DoesNotThrowAsync (async () => await writer.CloseAsync (TorrentFile));
            File.Delete (TorrentFile.FullPath);
        }

        [Test]
        public async Task CloseFileAsync_Unopened ()
        {
            await TestLoop.Current;

            using var writer = new DiskWriter ();
            Assert.DoesNotThrowAsync (async () => await writer.CloseAsync (TorrentFile));
        }

        [Test]
        public async Task TruncateLargeFile_ThenRead ()
        {
            await TestLoop.Current;

            Directory.CreateDirectory (Path.GetDirectoryName (TorrentFile.FullPath));
            using (var file = new FileStream (TorrentFile.FullPath, FileMode.OpenOrCreate))
                file.Write (new byte[TorrentFile.Length + 1]);

            // This should not implicitly truncate.
            // Some users will want to hashcheck some data and do not expect hash *checking*
            // to be a destructive operation. If the torrent actually begins downloading then
            // files should be truncated.
            using var writer = new DiskWriter ();
            await writer.ReadAsync (TorrentFile, 0, new byte[12]);
            Assert.AreEqual (TorrentFile.Length + 1, new FileInfo (TorrentFile.FullPath).Length);
        }

        [Test]
        public async Task TruncateLargeFile_ThenWrite ()
        {
            await TestLoop.Current;

            Directory.CreateDirectory (Path.GetDirectoryName (TorrentFile.FullPath));
            using (var file = new FileStream (TorrentFile.FullPath, FileMode.OpenOrCreate))
                file.Write (new byte[TorrentFile.Length + 1]);

            // File truncating only happens when starting a torrent in order to seed or leech it. It does not happen during hashchecking
            using var writer = new DiskWriter ();
            await writer.WriteAsync (TorrentFile, 0, new byte[12]);
            Assert.AreEqual (TorrentFile.Length + 1, new FileInfo (TorrentFile.FullPath).Length);
        }

        [Test]
        public async Task UnlimitedMaximumOpenFiles_Constructor ()
        {
            await TestLoop.Current;

            using var writer = new DiskWriter (0);
            await writer.WriteAsync (TorrentFile, 0, new byte[12]).WithTimeout ("timed out writing");
            Assert.AreEqual (12, new FileInfo (TorrentFile.FullPath).Length);
        }

        [Test]
        public async Task UnlimitedMaximumOpenFiles ()
        {
            await TestLoop.Current;

            using var writer = new DiskWriter ();
            await writer.SetMaximumOpenFilesAsync (0);
            await writer.WriteAsync (TorrentFile, 0, new byte[12]).WithTimeout ("timed out writing");
            Assert.AreEqual (12, new FileInfo (TorrentFile.FullPath).Length);
        }

        [Test]
        public async Task WriteMoreFilesThanMaximum()
        {
            await TestLoop.Current;

            using var writer = new DiskWriter (2);
            var files = TorrentFileInfo.Create (Constants.BlockSize * 4,
                Enumerable.Range (0, 10).Select (t => ($"test_{t}.file", (long) 12345, Path.Combine (Temp, $"test{t}.file"))).ToArray ()
            );

            var tasks = new System.Collections.Generic.List<Task> ();
            for (int i = 0; i < 100; i++)
                tasks.Add (writer.WriteAsync (Others[i % Others.Length], 0, new byte[TorrentFile.Length]).AsTask ());
            await Task.WhenAll (tasks);
        }
    }
}
