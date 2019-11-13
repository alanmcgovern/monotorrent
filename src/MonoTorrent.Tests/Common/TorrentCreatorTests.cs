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
        protected override IPieceWriter CreateReader()
        {
            TestWriter writer = new TestWriter();
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

        RawTrackerTiers announces;
        TestTorrentCreator creator;
        List<TorrentFile> files;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            HashAlgoFactory.Register<SHA1, SHA1Fake>();
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            HashAlgoFactory.Register<SHA1, SHA1CryptoServiceProvider>();
        }

        [SetUp]
        public void Setup()
        {
            creator = new TestTorrentCreator();
            announces = new RawTrackerTiers ();
            announces.Add(new RawTrackerTier (new string[] { "http://tier1.com/announce1", "http://tier1.com/announce2" }));
            announces.Add(new RawTrackerTier (new string[] { "http://tier2.com/announce1", "http://tier2.com/announce2" }));

            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;
            creator.SetCustom(CustomKey, CustomValue);
            files = new List<TorrentFile>(new TorrentFile[] { 
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File1"), (int)(PieceLength * 2.30), 0, 1),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File2"), (int)(PieceLength * 36.5), 1, 3),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir2"), "File3"), (int)(PieceLength * 3.17), 3, 12),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir1"), "File4"), (int)(PieceLength * 1.22), 12, 15),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir2"), "File5"), (int)(PieceLength * 6.94), 15, 15),
            });
        }

        [Test]
        public void AsyncCancel()
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
        public async Task CreateMultiTest()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            BEncodedDictionary dict = await creator.CreateAsync("TorrentName", files);
            Torrent torrent = Torrent.Load(dict);

            VerifyCommonParts(torrent);
            for (int i = 0; i < torrent.Files.Length; i++)
                Assert.IsTrue(files.Exists (delegate(TorrentFile f) { return f.Equals(torrent.Files[i]); }));
        }
        [Test]
        public async Task NoTrackersTest()
        {
            BEncodedDictionary dict = await creator.CreateAsync("TorrentName", files);
            Torrent t = Torrent.Load(dict);
            Assert.AreEqual(0, t.AnnounceUrls.Count, "#1");
        }

        [Test]
        public async Task CreateSingleTest()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            TorrentFile f = new TorrentFile(Path.GetFileName(files[0].Path),
                                            files[0].Length,
                                            files[0].StartPieceIndex,
                                            files[0].EndPieceIndex);

            BEncodedDictionary dict = await creator.CreateAsync(f.Path, new List<TorrentFile> (new TorrentFile[] { f }));
            Torrent torrent = Torrent.Load(dict);

            VerifyCommonParts(torrent);
            Assert.AreEqual(1, torrent.Files.Length, "#1");
            Assert.AreEqual(f, torrent.Files[0], "#2");
        }
        [Test]
        public void CreateSingleFromFolder()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            BEncodedDictionary dict = creator.Create(new TorrentFileSource(assembly.Location));

            Torrent t = Torrent.Load(dict);

            Assert.AreEqual(1, t.Files.Length, "#1");
            Assert.AreEqual(Path.GetFileName(assembly.Location), t.Name, "#2");
            Assert.AreEqual(Path.GetFileName(assembly.Location), t.Files[0].Path, "#3");

            // Create it again
            creator.Create(new TorrentFileSource(assembly.Location));
        }

        [Test]
        public async Task LargeMultiTorrent()
        {
            string name1 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            string name2 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            string name3 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            string name4 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            string name5 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            files = new List<TorrentFile>(new TorrentFile[] { 
                new TorrentFile(name1, (long)(PieceLength * 200.30), 0, 1),
                new TorrentFile(name2, (long)(PieceLength * 42000.5), 1, 3),
                new TorrentFile(name3, (long)(PieceLength * 300.17), 3, 12),
                new TorrentFile(name4, (long)(PieceLength * 100.22), 12, 15),
                new TorrentFile(name5, (long)(PieceLength * 600.94), 15, 15),
            });

            Torrent torrent = Torrent.Load (await creator.CreateAsync("BaseDir", files));
            Assert.AreEqual(5, torrent.Files.Length, "#1");
            Assert.AreEqual(name1, torrent.Files[0].Path, "#2");
            Assert.AreEqual(name2, torrent.Files[1].Path, "#3");
            Assert.AreEqual(name3, torrent.Files[2].Path, "#4");
            Assert.AreEqual(name4, torrent.Files[3].Path, "#5");
            Assert.AreEqual(name5, torrent.Files[4].Path, "#6");
        }

        [Test]
        public void IllegalDestinationPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var source = new CustomFileSource(new List<FileMapping> {
                    new FileMapping("a", "../../dest1"),
                });
                new TorrentCreator().Create(source);
            });
        }

        [Test]
        public void TwoFilesSameDestionation ()
        {
            Assert.Throws<ArgumentException> (() =>
            {
                var source = new CustomFileSource(new List<FileMapping> {
                    new FileMapping("a", "dest1"),
                    new FileMapping ("b", "dest2"),
                    new FileMapping ("c", "dest1"),
                });
                new TorrentCreator().Create(source);
            });
        }

        void VerifyCommonParts(Torrent torrent)
        {
            Assert.AreEqual(Comment, torrent.Comment, "#1");
            Assert.AreEqual(CreatedBy, torrent.CreatedBy, "#2");
            Assert.IsTrue((DateTime.UtcNow - torrent.CreationDate) < TimeSpan.FromSeconds(5), "#3");
            Assert.AreEqual(PieceLength, torrent.PieceLength, "#4");
            Assert.AreEqual(Publisher, torrent.Publisher, "#5");
            Assert.AreEqual(PublisherUrl, torrent.PublisherUrl, "#6");
            Assert.AreEqual(2, torrent.AnnounceUrls.Count, "#7");
            Assert.AreEqual(2, torrent.AnnounceUrls[0].Count, "#8");
            Assert.AreEqual(2, torrent.AnnounceUrls[1].Count, "#9");
        }
    }
}
