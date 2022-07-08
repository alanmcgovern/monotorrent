using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.PieceWriter;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        class Source : ITorrentFileSource
        {
            public IEnumerable<FileMapping> Files { get; set; }
            public string TorrentName { get; set; }
        }

        const string Comment = "My Comment";
        const string CreatedBy = "Created By MonoTorrent";
        const int PieceLength = 64 * 1024;
        const string Publisher = "My Publisher";
        const string PublisherUrl = "www.mypublisher.com";
        readonly BEncodedString CustomKey = "Custom Key";
        readonly BEncodedString CustomValue = "My custom value";

        List<List<string>> announces;
        TorrentCreator creator;
        Source files;

        Factories TestFactories => Factories.Default
            .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true });

        [SetUp]
        public void Setup ()
        {
            creator = new TorrentCreator (TorrentType.V1Only, TestFactories);
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
            files = new Source {
                TorrentName = "Name",
                Files = new[] {
                    new FileMapping (Path.Combine ("Dir1", "SDir1", "File1"), Path.Combine ("Dir1", "SDir1", "File1"), (long) (PieceLength * 2.30)),
                    new FileMapping (Path.Combine ("Dir1", "SDir1", "File2"), Path.Combine ("Dir1", "SDir1", "File2"), (long) (PieceLength * 36.5)),
                    new FileMapping (Path.Combine ("Dir1", "SDir2", "File3"), Path.Combine ("Dir1", "SDir2", "File3"), (long) (PieceLength * 3.17)),
                    new FileMapping (Path.Combine ("Dir2", "SDir1", "File4"), Path.Combine ("Dir2", "SDir1", "File4"), (long) (PieceLength * 1.22)),
                    new FileMapping (Path.Combine ("Dir2", "SDir2", "File5"), Path.Combine ("Dir2", "SDir2", "File5"), (long) (PieceLength * 6.94))
                }
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
            var torrentCreator = new TorrentCreator (TorrentType.V1Only, TestFactories);
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
                Assert.IsTrue (files.Files.Any (f => f.Destination.Equals (torrent.Files[i].Path)));
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

            var file = files.Files.First ();
            var source = new Source { TorrentName = files.TorrentName, Files = files.Files.Take (1) };

            BEncodedDictionary dict = await creator.CreateAsync (source);
            Torrent torrent = Torrent.Load (dict);

            VerifyCommonParts (torrent);
            Assert.AreEqual (1, torrent.Files.Count, "#1");
            Assert.AreEqual (0, torrent.Files[0].StartPieceIndex, "#1a");
            Assert.AreEqual (file.Length / creator.PieceLength, torrent.Files[0].EndPieceIndex, "#1b");
            Assert.AreEqual (file.Destination, torrent.Files[0].Path, "#2");
        }
        [Test]
        public void CreateSingleFromFolder ()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly ();
            BEncodedDictionary dict = creator.Create (new TorrentFileSource (assembly.Location));

            Torrent t = Torrent.Load (dict);

            Assert.AreEqual (1, t.Files.Count, "#1");
            Assert.AreEqual (0, t.Files[0].StartPieceIndex);
            Assert.AreNotEqual (0, t.Files[0].EndPieceIndex);
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Name, "#2");
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Files[0].Path, "#3");

            // Create it again
            creator.Create (new TorrentFileSource (assembly.Location));
        }

        [Test]
        public async Task CreateV2Torrent ()
        {
            // Create a torrent from files with all zeros
            // and see if it matches the one checked into the repo.
            var factories = Factories.Default
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true, FillValue = 0 });

            var creator = new TorrentCreator (TorrentType.V2Only, factories) {
                PieceLength = Constants.BlockSize * 4,
            };

            var files = Enumerable.Range (0, 24)
                .Select (t => new FileMapping ($"source_{t.ToString ().PadLeft (2, '0')}", $"dest_{t.ToString ().PadLeft (2, '0')}", (t + 1) * Constants.BlockSize))
                .ToList ();

            var torrentDict = await creator.CreateAsync (new CustomFileSource (files));
            var actual = Torrent.Load (torrentDict);
            var expected = Torrent.Load (Path.Combine (Path.GetDirectoryName (typeof(TorrentCreatorTests).Assembly.Location), $"test_torrent_64.torrent"));
            for (int i = 0; i < actual.Files.Count; i++) {
                Assert.AreEqual (expected.Files[i].PiecesRoot, actual.Files[i].PiecesRoot);
                Assert.AreEqual (expected.Files[i].Length, actual.Files[i].Length);
                Assert.AreEqual (expected.Files[i].Padding, actual.Files[i].Padding);
                Assert.AreEqual (expected.Files[i].StartPieceIndex, actual.Files[i].StartPieceIndex);
                Assert.AreEqual (expected.Files[i].EndPieceIndex, actual.Files[i].EndPieceIndex);
            }
        }
/*
 * test disabled because it takes 10+ seconds to run. It used to validate
 * that TorrentCreatore correctly handled files larger than 2GB, however
 * all of that core reading/writing logic is handled by IPieceWriter/DiskManager
 * now and so there's nothing really to test in this class.
 * Disable the test but don't delete it... because maybe it's good?
        [Test]
        public async Task LargeMultiTorrent ()
        {
            string name1 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File1");
            string name2 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File2");
            string name3 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File3");
            string name4 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File4");
            string name5 = Path.Combine (Path.Combine ("Dir1", "SDir1"), "File5");
            files = new Source {
                TorrentName = "name",
                Files = new[] {
                    new FileMapping (name1, name1, (long)(PieceLength * 200.30)),
                    new FileMapping (name2, name2, (long)(PieceLength * 42000.5)),
                    new FileMapping (name3, name3, (long)(PieceLength * 300.17)),
                    new FileMapping (name4, name4, (long)(PieceLength * 100.22)),
                    new FileMapping (name5, name5, (long)(PieceLength * 600.94))
                }
            };

            Torrent torrent = Torrent.Load (await creator.CreateAsync ("BaseDir", files));
            Assert.AreEqual (5, torrent.Files.Count, "#1");
            Assert.AreEqual (name1, torrent.Files[0].Path, "#2");
            Assert.AreEqual (name2, torrent.Files[1].Path, "#3");
            Assert.AreEqual (name3, torrent.Files[2].Path, "#4");
            Assert.AreEqual (name4, torrent.Files[3].Path, "#5");
            Assert.AreEqual (name5, torrent.Files[4].Path, "#6");
        }
*/
        [Test]
        public void IllegalDestinationPath ()
        {
            Assert.Throws<ArgumentException> (() => {
                var source = new Source {
                    TorrentName = "asd",
                    Files = new[] {
                        new FileMapping("a", "../../dest1", 123)
                    }
                };
                new TorrentCreator (TorrentType.V1Only, Factories.Default.WithPieceWriterCreator (files => new DiskWriter (files))).Create (source);
            });
        }

        [Test]
        public void TwoFilesSameDestination ()
        {
            Assert.Throws<ArgumentException> (() => {
                var source = new Source {
                    TorrentName = "asd",
                    Files = new[] {
                        new FileMapping ("a", "dest1", 123),
                        new FileMapping ("b", "dest2", 345),
                        new FileMapping ("c", "dest1", 453)
                    }
                };
                new TorrentCreator (TorrentType.V1Only, Factories.Default).Create (source);
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
