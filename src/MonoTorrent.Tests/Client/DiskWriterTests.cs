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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    public class DiskWriterTests
    {
        string Temp { get; set; }
        ITorrentFileInfo[] Others { get; set; }
        ITorrentFileInfo TorrentFile { get; set; }

        [SetUp]
        public void Setup ()
        {
            Temp = Path.GetTempPath () + "monotorrent_tests";
            TorrentFile = new TorrentFileInfo (new TorrentFile ("test.file", 12345), Path.Combine (Temp, "test.file"));
            Others = new[] {
                new TorrentFileInfo (new TorrentFile ("test2.file", 12345), Path.Combine (Temp, "test2.file")),
                new TorrentFileInfo (new TorrentFile ("test3.file", 12345), Path.Combine (Temp, "test3.file")),
                new TorrentFileInfo (new TorrentFile ("test4.file", 12345), Path.Combine (Temp, "test4.file")),
            };
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
            using var writer = new DiskWriter ();
            await writer.WriteAsync (TorrentFile, 0, new byte[10], 0, 10);
            Assert.IsTrue (File.Exists (TorrentFile.FullPath));

            Assert.DoesNotThrowAsync (async () => await writer.CloseAsync (TorrentFile));
            File.Delete (TorrentFile.FullPath);
        }

        [Test]
        public void CloseFileAsync_Unopened()
        {
            using var writer = new DiskWriter ();
            Assert.DoesNotThrowAsync (async () => await writer.CloseAsync (TorrentFile));
        }

        [Test]
        public async Task ExceedMaxOpenFiles ()
        {
            var streams = new List<ManualStream> ();
            var streamCreated = new ReusableTaskCompletionSource<bool> ();
            Func<ITorrentFileInfo, FileAccess, ITorrentFileStream> creator = (file, access) => {
                var s = new ManualStream (file, access);
                s.WriteTcs = new ReusableTaskCompletionSource<int> ();
                streams.Add (s);
                streamCreated.SetResult (true);
                return s;
            };
            using var writer = new DiskWriter (creator, 1);

            var writeTask = writer.WriteAsync (TorrentFile, 0, new byte[100], 0, 100);
            await streamCreated.Task.WithTimeout ();

            // There's a limit of 1 concurrent read/write.
            var secondStreamWaiter = streamCreated.Task.AsTask ();
            var secondStream = writer.WriteAsync (Others.First (), 0, new byte[100], 0, 100);
            Assert.ThrowsAsync<TimeoutException> (() => secondStreamWaiter.WithTimeout (100));

            streams[0].WriteTcs.SetResult (1);
            await secondStreamWaiter.WithTimeout ();
            streams[1].WriteTcs.SetResult (1);

            await secondStream.WithTimeout ();
            Assert.IsTrue (streams[0].Disposed);
            Assert.IsFalse (streams[1].Disposed);
        }
    }
}
