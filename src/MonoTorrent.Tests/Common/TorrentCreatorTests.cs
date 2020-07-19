using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    public class TestTorrentCreator : TorrentCreator
    {
        protected override IPieceWriter CreateReader ()
        {
            TestWriter writer = new TestWriter ();
            writer.DontWrite = true;
            return writer;
        }
    }

    [TestFixture]
    public class TorrentCreatorTests
    {
        const string Comment = "My Comment";
        const string CreatedBy = "Created By MonoTorrent";
        const int PieceLength = 64 * 1024;
        const string Publisher = "My Publisher";
        const string PublisherUrl = "www.mypublisher.com";
        readonly BEncodedString CustomKey = "Custom Key";
        readonly BEncodedString CustomValue = "My custom value";

        List<List<string>> announces;
        TestTorrentCreator creator;
        List<TorrentCreator.InputFile> files;

        [OneTimeSetUp]
        public void FixtureSetup ()
        {
            HashAlgoFactory.SHA1Builder = () => new SHA1Fake ();
        }

        [OneTimeTearDown]
        public void FixtureTeardown ()
        {
            HashAlgoFactory.SHA1Builder = () => SHA1.Create ();
        }

        [SetUp]
        public void Setup ()
        {
            creator = new TestTorrentCreator ();
            announces = new List<List<string>> {
                new List<string> (new[] { "http://tier1.com/announce1", "http://tier1.com/announce2" }),
                new List<string> (new[] { "http://tier2.com/announce1", "http://tier2.com/announce2" })
            };

            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;
            creator.SetCustom (CustomKey, CustomValue);
            files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File1"), (int)(PieceLength * 2.30)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File2"), (int)(PieceLength * 36.5)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir2", "File3"), (int)(PieceLength * 3.17)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir1", "File4"), (int)(PieceLength * 1.22)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir2", "File5"), (int)(PieceLength * 6.94)),
            };
        }

        [Test]
        public void AsyncCancel ()
        {
            var cts = new CancellationTokenSource ();
            cts.Cancel ();

            var fileSource = new TorrentFileSource (typeof (TorrentCreatorTests).Assembly.Location);
            Assert.ThrowsAsync<OperationCanceledException> (() => creator.CreateAsync (fileSource, cts.Token));
        }

        [Test]
        public void AutoSelectPieceLength ()
        {
            var torrentCreator = new TestTorrentCreator ();
            Assert.DoesNotThrowAsync (() => torrentCreator.CreateAsync ("name", files, CancellationToken.None));
        }

        [Test]
        public async Task CreateMultiTest ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", files);
            Torrent torrent = Torrent.Load (dict);

            VerifyCommonParts (torrent);
            for (int i = 0; i < torrent.Files.Count; i++)
                Assert.IsTrue (files.Exists (f => f.Path.Equals (torrent.Files[i].Path)));
        }
        [Test]
        public async Task AnnounceUrl_None ()
        {
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", files);
            Torrent t = Torrent.Load (dict);
            Assert.IsFalse (dict.ContainsKey ("announce-list"));
            Assert.IsFalse (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task AnnounceUrl_Primary ()
        {
            creator.Announce = "http://127.0.0.1:12345/announce";
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", files);
            Assert.IsFalse (dict.ContainsKey ("announce-list"));
            Assert.IsTrue (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task AnnounceUrl_ManyTiers ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", files);
            Assert.IsTrue (dict.ContainsKey ("announce-list"));
            Assert.IsFalse (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task CreateSingleTest ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            var f = new TorrentCreator.InputFile (Path.GetFileName (files[0].Path),
                                            files[0].Length);

            BEncodedDictionary dict = await creator.CreateAsync (f.Path, new List<TorrentCreator.InputFile> (new[] { f }));
            Torrent torrent = Torrent.Load (dict);

            VerifyCommonParts (torrent);
            Assert.AreEqual (1, torrent.Files.Count, "#1");
            Assert.AreEqual (f.Path, torrent.Files[0].Path, "#2");
        }
        [Test]
        public void CreateSingleFromFolder ()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly ();
            BEncodedDictionary dict = creator.Create (new TorrentFileSource (assembly.Location));

            Torrent t = Torrent.Load (dict);

            Assert.AreEqual (1, t.Files.Count, "#1");
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Name, "#2");
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Files[0].Path, "#3");

            // Create it again
            creator.Create (new TorrentFileSource (assembly.Location));
        }

        [Test]
        public async Task LargeMultiTorrent ()
        {
            string name1 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File1");
            string name2 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File2");
            string name3 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File3");
            string name4 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File4");
            string name5 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File5");
            files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(name1, (long)(PieceLength * 200.30)),
                new TorrentCreator.InputFile(name2, (long)(PieceLength * 42000.5)),
                new TorrentCreator.InputFile(name3, (long)(PieceLength * 300.17)),
                new TorrentCreator.InputFile(name4, (long)(PieceLength * 100.22)),
                new TorrentCreator.InputFile(name5, (long)(PieceLength * 600.94)),
            };

            Torrent torrent = Torrent.Load (await creator.CreateAsync ("BaseDir", files));
            Assert.AreEqual (5, torrent.Files.Count, "#1");
            Assert.AreEqual (name1, torrent.Files[0].Path, "#2");
            Assert.AreEqual (name2, torrent.Files[1].Path, "#3");
            Assert.AreEqual (name3, torrent.Files[2].Path, "#4");
            Assert.AreEqual (name4, torrent.Files[3].Path, "#5");
            Assert.AreEqual (name5, torrent.Files[4].Path, "#6");
        }

        [Test]
        public void IllegalDestinationPath ()
        {
            Assert.Throws<ArgumentException> (() => {
                var source = new CustomFileSource (new List<FileMapping> {
                    new FileMapping("a", "../../dest1"),
                });
                new TorrentCreator ().Create (source);
            });
        }

        [Test]
        public void TwoFilesSameDestination ()
        {
            Assert.Throws<ArgumentException> (() => {
                var source = new CustomFileSource (new List<FileMapping> {
                    new FileMapping("a", "dest1"),
                    new FileMapping ("b", "dest2"),
                    new FileMapping ("c", "dest1"),
                });
                new TorrentCreator ().Create (source);
            });
        }

        void VerifyCommonParts (Torrent torrent)
        {
            Assert.AreEqual (Comment, torrent.Comment, "#1");
            Assert.AreEqual (CreatedBy, torrent.CreatedBy, "#2");
            Assert.IsTrue ((DateTime.UtcNow - torrent.CreationDate) < TimeSpan.FromSeconds (5), "#3");
            Assert.AreEqual (PieceLength, torrent.PieceLength, "#4");
            Assert.AreEqual (Publisher, torrent.Publisher, "#5");
            Assert.AreEqual (PublisherUrl, torrent.PublisherUrl, "#6");
            Assert.AreEqual (2, torrent.AnnounceUrls.Count, "#7");
            Assert.AreEqual (2, torrent.AnnounceUrls[0].Count, "#8");
            Assert.AreEqual (2, torrent.AnnounceUrls[1].Count, "#9");
        }
    }
}
