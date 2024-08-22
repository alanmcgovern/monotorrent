//
// StreamProviderTests.cs
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.Streaming
{
    [TestFixture]
    public class StreamProviderTests
    {
        ClientEngine Engine { get; set; }
        MagnetLink MagnetLink { get; set; }
        TestWriter PieceWriter { get; set; }
        BEncodedDictionary torrentInfo;
        Torrent Torrent { get; set; }

        [SetUp]
        public void Setup ()
        {
            PieceWriter = new TestWriter ();

            Engine = EngineHelpers.Create (EngineHelpers.CreateSettings (), EngineHelpers.Factories.WithPieceWriterCreator (t => PieceWriter));
            Torrent = TestRig.CreateMultiFileTorrent (new[] { new TorrentFile ("path", Constants.BlockSize * 1024, 0, 1024 / 8 - 1, 0, TorrentFileAttributes.None, 0) }, Constants.BlockSize * 8, out torrentInfo);
            MagnetLink = new MagnetLink (Torrent.InfoHashes, "MagnetDownload");
        }

        [TearDown]
        public async Task Teardown ()
        {
            await Engine.StopAllAsync ().WithTimeout ();
            Engine.Dispose ();
        }

        [Test]
        public async Task CreateStream ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "test");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None);
            Assert.IsNotNull (stream);
            Assert.AreEqual (0, stream.Position);
            Assert.AreEqual (manager.Files[0].Length, stream.Length);
        }

        [Test]
        public async Task CreateStream_Prebuffer ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "test");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: true, CancellationToken.None).WithTimeout ();
            Assert.IsNotNull (stream);
            Assert.AreEqual (0, stream.Position);
            Assert.AreEqual (manager.Files[0].Length, stream.Length);
        }

        [Test]
        public async Task ReadPastEndOfStream ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None).WithTimeout ();
            stream.Seek (0, SeekOrigin.End);
            Assert.AreEqual (0, await stream.ReadAsync (new byte[1], 0, 1).WithTimeout ());

            stream.Seek (-1, SeekOrigin.End);
            Assert.AreEqual (1, await stream.ReadAsync (new byte[1], 0, 2).WithTimeout ());

        }

        [Test]
        public async Task ReadLastByte ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None).WithTimeout ();
            stream.Seek (-1, SeekOrigin.End);
            Assert.AreEqual (1, await stream.ReadAsync (new byte[1], 0, 1).WithTimeout ());

            stream.Seek (-1, SeekOrigin.End);
            Assert.AreEqual (1, await stream.ReadAsync (new byte[5], 0, 5).WithTimeout ());
        }

        [Test]
        public async Task SeekBeforeStart ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None).WithTimeout ();
            stream.Seek (-100, SeekOrigin.Begin);
            Assert.AreEqual (0, stream.Position);
        }

        [Test]
        public async Task SeekToMiddle ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None);
            stream.Seek (12345, SeekOrigin.Begin);
        }

        [Test]
        public async Task SeekPastEnd ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], prebuffer: false, CancellationToken.None).WithTimeout ();
            stream.Seek (stream.Length + 100, SeekOrigin.Begin);
            Assert.AreEqual (stream.Length, stream.Position);
        }

        [Test]
        public async Task CreateStreamBeforeStart ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            Assert.ThrowsAsync<InvalidOperationException> (() => manager.StreamProvider.CreateHttpStreamAsync (manager.Files[0]));
        }

        [Test]
        public async Task CreateStreamTwice ()
        {
            var manager = await Engine.AddStreamingAsync (Torrent, "testDir");
            await manager.LoadFastResumeAsync (new FastResume (manager.InfoHashes, new BitField (manager.Torrent.PieceCount ()).SetAll (true), new BitField (manager.Torrent.PieceCount ())));
            await PieceWriter.CreateAsync (manager.Files);

            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Seeding);

            using var stream = await manager.StreamProvider.CreateStreamAsync (manager.Files[0], false, CancellationToken.None);
            Assert.ThrowsAsync<InvalidOperationException> (() => manager.StreamProvider.CreateStreamAsync (manager.Files[0], false, CancellationToken.None));
        }
    }
}
