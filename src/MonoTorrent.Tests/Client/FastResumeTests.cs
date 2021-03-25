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

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class FastResumeTests
    {
        static readonly InfoHash InfoHash = new InfoHash (new byte[20]);

        [Test]
        public void AllHashed_AllDownloaded ()
        {
            var unhashedPieces = new MutableBitField (10).SetAll (false);
            var downloaded = new MutableBitField (10).SetAll (true);
            Assert.DoesNotThrow (() => new FastResume (InfoHash, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void AllHashed_NothingDownloaded ()
        {
            var unhashedPieces = new MutableBitField (10).SetAll (false);
            var downloaded = new MutableBitField (10).SetAll (false);
            Assert.DoesNotThrow (() => new FastResume (InfoHash, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void NoneHashed_NothingDownloaded ()
        {
            var unhashedPieces = new MutableBitField (10).SetAll (true);
            var downloaded = new MutableBitField (10).SetAll (false);
            Assert.DoesNotThrow (() => new FastResume (InfoHash, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void NoneHashed_AllDownloaded ()
        {
            var unhashedPieces = new MutableBitField (10).SetAll (true);
            var downloaded = new MutableBitField (10).SetAll (true);
            Assert.Throws<ArgumentException> (() => new FastResume (InfoHash, downloaded, unhashedPieces), "#1");
        }

        [Test]
        public void LoadV1FastResumeData ()
        {
            var v1Data = new BEncodedDictionary {
                { FastResume.VersionKey, (BEncodedNumber)1 },
                { FastResume.InfoHashKey, new BEncodedString(InfoHash.Hash) },
                { FastResume.BitfieldKey, new BEncodedString(new MutableBitField (10).SetAll (true).ToByteArray ()) },
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
                { FastResume.InfoHashKey, new BEncodedString(InfoHash.Hash) },
                { FastResume.BitfieldKey, new BEncodedString(new MutableBitField (10).SetAll (false).Set (0, true).ToByteArray ()) },
                { FastResume.BitfieldLengthKey, (BEncodedNumber)10 },
                { FastResume.UnhashedPiecesKey, new BEncodedString (new MutableBitField (10).SetAll (true).Set (0, false).ToByteArray ()) },
            };

            // If this is a v1 FastResume data then it comes from a version of MonoTorrent which always
            // hashes the entire file.
            var fastResume = new FastResume (v1Data);
            Assert.AreEqual (1, fastResume.Bitfield.TrueCount, "#1");
            Assert.AreEqual (9, fastResume.UnhashedPieces.TrueCount, "#2");
        }

        [Test]
        public async Task IgnoreInvalidFastResume ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = new ClientEngine (new EngineSettingsBuilder (EngineSettingsBuilder.CreateForTests ()) {
                AutoSaveLoadFastResume = true,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHash);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (InfoHash, new MutableBitField (torrent.Pieces.Count).SetAll (false), new MutableBitField (torrent.Pieces.Count)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            Assert.IsFalse (manager.HashChecked);
            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Downloading);
            Assert.IsFalse (File.Exists (path));
        }

        [Test]
        public async Task DeleteAfterDownloading ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = new ClientEngine (new EngineSettingsBuilder (EngineSettingsBuilder.CreateForTests ()) {
                AutoSaveLoadFastResume = true,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHash);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHash, new MutableBitField (torrent.Pieces.Count).SetAll (false), new MutableBitField (torrent.Pieces.Count)).Encode ());
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
            using var engine = new ClientEngine (new EngineSettingsBuilder (EngineSettingsBuilder.CreateForTests ()) {
                AutoSaveLoadFastResume = true,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHash);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHash, new MutableBitField (torrent.Pieces.Count).SetAll (true), new BitField (torrent.Pieces.Count)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            Assert.IsTrue (manager.HashChecked);
            await manager.StartAsync ();
            await manager.WaitForState (TorrentState.Downloading);
            Assert.IsFalse (File.Exists (path));

        }

        [Test]
        public async Task DeleteBeforeHashing ()
        {
            using var tmpDir = TempDir.Create ();
            using var engine = new ClientEngine (new EngineSettingsBuilder (EngineSettingsBuilder.CreateForTests ()) {
                AutoSaveLoadFastResume = true,
                CacheDirectory = tmpDir.Path,
            }.ToSettings ());

            var first = new TaskCompletionSource<object> ();
            var second = new TaskCompletionSource<object> ();

            var torrent = TestRig.CreatePrivate ();
            var path = engine.Settings.GetFastResumePath (torrent.InfoHash);
            Directory.CreateDirectory (Path.GetDirectoryName (path));
            File.WriteAllBytes (path, new FastResume (torrent.InfoHash, new MutableBitField (torrent.Pieces.Count).SetAll (true), new MutableBitField (torrent.Pieces.Count)).Encode ());
            var manager = await engine.AddAsync (torrent, "savedir");
            await engine.ChangePieceWriterAsync (new TestWriter {
                FilesThatExist = new System.Collections.Generic.List<ITorrentFileInfo> (manager.Files)
            });

            Assert.IsTrue (manager.HashChecked);
            manager.Engine.DiskManager.GetHashAsyncOverride = (torrent, pieceIndex) => {
                first.SetResult (null);
                second.Task.Wait ();
                return new byte[20];
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
