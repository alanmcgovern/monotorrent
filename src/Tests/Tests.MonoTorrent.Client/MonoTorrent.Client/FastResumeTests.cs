//
// FastResumeTests.cs
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
using System.IO;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Common;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class FastResumeTests
    {
        static InfoHash InfoHash => InfoHashes.V1;
        static InfoHashes InfoHashes { get; } = new InfoHashes (new InfoHash (new byte[20]), null);

        [Test]
        public void AllHashed_AllDownloaded ()
        {
            var unhashedPieces = new BitField (10).SetAll (false);
            var downloaded = new BitField (10).SetAll (true);
            Assert.DoesNotThrow (() => new FastResume (InfoHashes, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void AllHashed_NothingDownloaded ()
        {
            var unhashedPieces = new BitField (10).SetAll (false);
            var downloaded = new BitField (10).SetAll (false);
            Assert.DoesNotThrow (() => new FastResume (InfoHashes, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void NoneHashed_NothingDownloaded ()
        {
            var unhashedPieces = new BitField (10).SetAll (true);
            var downloaded = new BitField (10).SetAll (false);
            Assert.DoesNotThrow (() => new FastResume (InfoHashes, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void NoneHashed_AllDownloaded ()
        {
            var unhashedPieces = new BitField (10).SetAll (true);
            var downloaded = new BitField (10).SetAll (true);
            Assert.Throws<ArgumentException> (() => new FastResume (InfoHashes, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void LoadV1FastResumeData ()
        {
            var v1Data = new BEncodedDictionary {
                { FastResume.VersionKey, (BEncodedNumber)1 },
                { FastResume.InfoHashKey, new BEncodedString(InfoHash.Span.ToArray ()) },
                { FastResume.BitfieldKey, new BEncodedString(new BitField (10).SetAll (true).ToBytes ()) },
                { FastResume.BitfieldLengthKey, (BEncodedNumber)10 },
            };

            // If this is a v1 FastResume data then it comes from a version of MonoTorrent which always
            // hashes the entire file.
            var fastResume = new FastResume (v1Data);
            Assert.IsTrue (fastResume.UnhashedPieces.AllFalse, "#1");
            Assert.IsTrue (fastResume.Bitfield.AllTrue, "#2");
        }

        [Test]
        public void LoadV2FastResumeData ()
        {
            var v1Data = new BEncodedDictionary {
                { FastResume.VersionKey, (BEncodedNumber)1 },
                { FastResume.InfoHashKey, new BEncodedString(InfoHash.Span.ToArray ()) },
                { FastResume.BitfieldKey, new BEncodedString(new BitField (10).SetAll (false).Set (0, true).ToBytes ()) },
                { FastResume.BitfieldLengthKey, (BEncodedNumber)10 },
                { FastResume.UnhashedPiecesKey, new BEncodedString (new BitField (10).SetAll (true).Set (0, false).ToBytes ()) },
            };

            // If this is a v1 FastResume data then it comes from a version of MonoTorrent which always
            // hashes the entire file.
            var fastResume = new FastResume (v1Data);
            Assert.AreEqual (1, fastResume.Bitfield.TrueCount, "#1");
            Assert.AreEqual (9, fastResume.UnhashedPieces.TrueCount, "#2");
        }

        [Test]
        public void LoadEncoded ()
        {
            var unhashedPieces = new BitField (10).SetAll (false);
            var downloaded = new BitField (10).SetAll (true);
            var fastResume = new FastResume (InfoHashes, downloaded, unhashedPieces);
            var stream = new MemoryStream ();

            fastResume.Encode (stream);
            Assert.IsTrue (stream.Length > 0, "#1");

            stream.Seek(0, SeekOrigin.Begin);
            Assert.IsTrue (FastResume.TryLoad (stream, out var newFastResume), "#2");
            Assert.IsNotNull (newFastResume, "#3");
            Assert.IsTrue (newFastResume.UnhashedPieces.AllFalse, "#4");
            Assert.IsTrue (newFastResume.Bitfield.AllTrue, "#5");
        }

        [Test]
        public async Task IgnoreInvalidFastResume ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AutoSaveLoadFastResume = true,
                FastResumeMode = FastResumeMode.Accurate,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHashes);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (new InfoHashes (InfoHash, null), new BitField (torrent.PieceCount).SetAll (false), new BitField (torrent.PieceCount)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            Assert.IsFalse (manager.HashChecked);
            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Downloading);
            Assert.IsFalse (File.Exists (path));
        }

        [Test]
        public async Task CanExplicitlyLoadHybridTorrentFastResume ()
        {
            // Ensure the data can be saved/loaded, including when converted to a byte[] for storage on disk.
            using var engine = EngineHelpers.Create ();
            var torrent = Torrent.Load (Path.Combine (Path.GetDirectoryName (typeof (TorrentV2Test).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent"));
            var manager = await engine.AddAsync (torrent, "");

            await manager.HashCheckAsync (false);
            var fastResume = await manager.SaveFastResumeAsync ();

            await manager.SetNeedsHashCheckAsync ();
            await manager.LoadFastResumeAsync (fastResume);
            Assert.IsTrue (manager.HashChecked);

            await manager.SetNeedsHashCheckAsync ();
            await manager.LoadFastResumeAsync (new FastResume (BEncodedValue.Decode<BEncodedDictionary> (fastResume.Encode ())));
        }

        [Test]
        public async Task CanImplicitlyLoadHybridTorrentFastResume ()
        {
            using var tmpDir = TempDir.Create ();
            var settings = EngineHelpers.CreateSettings(cacheDirectory: tmpDir.Path, automaticFastResume: true);
            // Ensure the on-disk cache can be loaded implicitly when loading a torrent into the engine
            using var engine = EngineHelpers.Create (settings);
            var torrent = Torrent.Load (Path.Combine (Path.GetDirectoryName (typeof (TorrentV2Test).Assembly.Location), "MonoTorrent", "bittorrent-v2-hybrid-test.torrent"));
            var manager = await engine.AddAsync (torrent, "");
            Assert.IsFalse (manager.HashChecked);

            // Hash check and write fast resume data to disk
            await manager.HashCheckAsync (false);

            await engine.RemoveAsync (manager, RemoveMode.KeepAllData);
            manager = await engine.AddAsync (torrent, "");
            Assert.IsTrue (manager.HashChecked);
        }

        [Test]
        public async Task DeleteAfterDownloading ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AutoSaveLoadFastResume = true,
                FastResumeMode = FastResumeMode.Accurate,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHashes);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHashes, new BitField (torrent.PieceCount).SetAll (false), new BitField (torrent.PieceCount)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            Assert.IsTrue (manager.HashChecked);
            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Downloading);
            Assert.IsFalse (File.Exists (path));
        }

        [Test]
        public async Task RetainAfterSeeding ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AutoSaveLoadFastResume = true,
                FastResumeMode = FastResumeMode.Accurate,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHashes);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHashes, new BitField (torrent.PieceCount).SetAll (true), new ReadOnlyBitField (torrent.PieceCount)).Encode ());
            var manager = await engine.AddAsync (torrent, Path.Combine(tmpDir.Path, "savedir"));
            Assert.IsTrue (manager.HashChecked);
            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Downloading);
            Assert.IsFalse (File.Exists (path));

        }

        [Test]
        public async Task DeleteBeforeHashing ()
        {
            TestWriter testWriter = null;
            using var tmpDir = TempDir.Create ();
            using var engine = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AutoSaveLoadFastResume = true,
                CacheDirectory = tmpDir.Path,
            }.ToSettings (),
                Factories.Default.WithPieceWriterCreator (maxOpenFiles => (testWriter = new TestWriter ()))
            );

            var first = new TaskCompletionSource<object> ();
            var second = new TaskCompletionSource<object> ();

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHashes);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHashes, new BitField (torrent.PieceCount).SetAll (true), new BitField (torrent.PieceCount)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            await testWriter.CreateAsync (manager.Files);

            Assert.IsTrue (manager.HashChecked);
            manager.Engine.DiskManager.GetHashAsyncOverride = (torrent, pieceIndex, dest) => {
                first.SetResult (null);
                second.Task.Wait ();
                new byte[20].CopyTo (dest.V1Hash);
                return Task.FromResult (true);
            };
            var hashCheckTask = manager.HashCheckAsync (false);
            await first.Task.WithTimeout ();
            Assert.IsFalse (File.Exists (path));

            second.SetResult (null);
            await hashCheckTask.WithTimeout ();
            Assert.IsTrue (File.Exists (path));
        }
    }
}
