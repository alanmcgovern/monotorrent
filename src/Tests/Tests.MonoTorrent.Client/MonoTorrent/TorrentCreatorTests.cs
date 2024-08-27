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
        public class CapturingTorrentCreator : TorrentCreator
        {
            public Dictionary<string, List<TorrentCreatorEventArgs>> HashedEventArgs = new Dictionary<string, List<TorrentCreatorEventArgs>> ();
            public CapturingTorrentCreator (TorrentType type, Factories factories)
                : base(type, factories)
            {

            }

            protected override void OnHashed (TorrentCreatorEventArgs e)
            {
                if (!HashedEventArgs.TryGetValue (e.CurrentFile, out List<TorrentCreatorEventArgs> value))
                    HashedEventArgs[e.CurrentFile] = value = new List<TorrentCreatorEventArgs> ();
                value.Add (e);
            }
        }

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
        CapturingTorrentCreator creator;
        Source filesSource;

        Factories TestFactories => EngineHelpers.Factories
            .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true });

        [SetUp]
        public void Setup ()
        {
            creator = new CapturingTorrentCreator (TorrentType.V1Only, TestFactories);
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
            filesSource = new Source {
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
            Assert.DoesNotThrowAsync (() => torrentCreator.CreateAsync ("name", filesSource, CancellationToken.None));
        }

        [Test]
        public async Task CreateMultiTest ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", filesSource);
            Torrent torrent = Torrent.Load (dict);

            VerifyCommonParts (torrent);
            for (int i = 0; i < torrent.Files.Count; i++)
                Assert.IsTrue (filesSource.Files.Any (f => f.Destination.Equals (torrent.Files[i].Path)));
        }

        [Test]
        public async Task CreateMultiTest_EmitsHashEvents ()
        {
            await creator.CreateAsync ("TorrentName", filesSource);

            var files = filesSource.Files.ToList ();
            var hashes = creator.HashedEventArgs;
            Assert.AreEqual (hashes.Count, files.Count);


            foreach (var file in files) {
                Assert.IsTrue (hashes[file.Source].Any (t => t.FileBytesHashed == t.FileSize));
                Assert.IsTrue (hashes[file.Source].All (t => t.FileBytesHashed <= t.FileSize));
                Assert.IsTrue (hashes[file.Source].All (t => t.FileBytesHashed > 0));
            }
        }

        [Test]
        public async Task AnnounceUrl_None ()
        {
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", filesSource);
            Torrent t = Torrent.Load (dict);
            Assert.IsFalse (dict.ContainsKey ("announce-list"));
            Assert.IsFalse (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task AnnounceUrl_Primary ()
        {
            creator.Announce = "http://127.0.0.1:12345/announce";
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", filesSource);
            Assert.IsFalse (dict.ContainsKey ("announce-list"));
            Assert.IsTrue (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task AnnounceUrl_ManyTiers ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);
            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", filesSource);
            Assert.IsTrue (dict.ContainsKey ("announce-list"));
            Assert.IsFalse (dict.ContainsKey ("announce"));
        }

        [Test]
        public async Task CreateSingleTest ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            var file = filesSource.Files.First ();
            var source = new Source { TorrentName = filesSource.TorrentName, Files = filesSource.Files.Take (1) };

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
            var factories = TestFactories
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true, FillValue = 0 });

            var creator = new TorrentCreator (TorrentType.V2Only, factories) {
                PieceLength = Constants.BlockSize * 4,
            };

            var files = Enumerable.Range (0, 24)
                .Select (t => new FileMapping ($"source_{t.ToString ().PadLeft (2, '0')}", $"dest_{t.ToString ().PadLeft (2, '0')}", (t + 1) * Constants.BlockSize))
                .ToList ();

            var torrentDict = await creator.CreateAsync (new CustomFileSource (files));
            var infoDict = torrentDict["info"] as BEncodedDictionary;
            Assert.AreEqual ((BEncodedNumber)2, infoDict["meta version"]);

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

        [Test]
        public async Task CreateV2Torrent_WithExtraEmptyFile ()
        {
            // Create a torrent from files with all zeros
            // and see if it matches the one checked into the repo.
            var factories = TestFactories
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true, FillValue = 0 });

            var creator = new TorrentCreator (TorrentType.V2Only, factories) {
                PieceLength = Constants.BlockSize * 4,
            };

            var files = Enumerable.Range (0, 24)
                .Select (t => new FileMapping ($"source_{t.ToString ().PadLeft (2, '0')}", $"dest_{t.ToString ().PadLeft (2, '0')}", (t + 1) * Constants.BlockSize))
                .ToList ();
            files.Add (new FileMapping ("empty_source", "empty_dest", 0));

            var torrentDict = await creator.CreateAsync (new CustomFileSource (files));
            var fileTree = (BEncodedDictionary) ((BEncodedDictionary) torrentDict["info"])["file tree"];

            // Get the metadata for this file specifically. It should only have a length of zero, nothing else.
            var emptyFile = (BEncodedDictionary) ((BEncodedDictionary) fileTree["empty_dest"])[""];
            Assert.IsFalse (emptyFile.ContainsKey ("pieces root"));
            Assert.AreEqual (new BEncodedNumber(0), emptyFile["length"]);

            var actual = Torrent.Load (torrentDict);
            var expected = Torrent.Load (Path.Combine (Path.GetDirectoryName (typeof (TorrentCreatorTests).Assembly.Location), $"test_torrent_64.torrent"));

            // The first file is empty.
            Assert.IsTrue (actual.Files.First ().PiecesRoot.IsEmpty);
            Assert.AreEqual (0, actual.Files.First ().Length);
            Assert.AreEqual (0, actual.Files.First ().Padding);
            Assert.AreEqual (0, actual.Files.First ().OffsetInTorrent);

            for (int i = 0; i < expected.Files.Count; i++) {
                Assert.AreEqual (expected.Files[i].PiecesRoot, actual.Files[i + 1].PiecesRoot);
                Assert.AreEqual (expected.Files[i].Length, actual.Files[i + 1].Length);
                Assert.AreEqual (expected.Files[i].Padding, actual.Files[i + 1].Padding);
                Assert.AreEqual (expected.Files[i].StartPieceIndex, actual.Files[i + 1].StartPieceIndex);
                Assert.AreEqual (expected.Files[i].EndPieceIndex, actual.Files[i + 1].EndPieceIndex);
            }
        }

        [Test]
        public async Task CreateV2Torrent_SortFilesCorrectly ()
        {
            var dir = Path.Combine (Path.Combine ("foo", "bar", "baz"));
            var fileSource = new CustomFileSource (new List<FileMapping> {
                new FileMapping (Path.Combine (dir, "A.file"), "A", 4),
                new FileMapping (Path.Combine (dir, "C.file"), "C",  4),
                new FileMapping (Path.Combine (dir, "B.file"), "B",  4),
            });

            TorrentCreator torrentCreator = new TorrentCreator (TorrentType.V1V2Hybrid, TestFactories);
            var encodedTorrent = await torrentCreator.CreateAsync (fileSource);
            var torrent = Torrent.Load (encodedTorrent);

            Assert.IsNotNull (torrent);
            Assert.AreEqual ("A", torrent.Files[0].Path);
            Assert.AreEqual ("B", torrent.Files[1].Path);
            Assert.AreEqual ("C", torrent.Files[2].Path);
        }

        [Test]
        public async Task CreateHybridTorrent_SortFilesCorrectly ()
        {
            var destFiles = new[] {
                "A.txt",
                "B.txt",
                Path.Combine ("D", "a", "A.txt"),
                Path.Combine("a", "z", "Z.txt"),
            };

            var dir = Path.Combine (Path.Combine ("foo", "bar", "baz"));
            var fileSource = new CustomFileSource (destFiles.Select (t =>
                new FileMapping (Path.Combine (dir, t), t, 4)
            ).ToList ());

            TorrentCreator torrentCreator = new TorrentCreator (TorrentType.V1V2Hybrid, TestFactories);
            var torrent = await torrentCreator.CreateAsync (fileSource);

            var fileTree = (BEncodedDictionary) ((BEncodedDictionary) torrent["info"])["file tree"];

            // Ensure the directory tree was converted into a dictionary tree.
            Assert.IsTrue (fileTree.ContainsKey ("A.txt"));
            Assert.IsTrue (fileTree.ContainsKey ("D"));
            Assert.IsTrue (fileTree.ContainsKey ("a"));

            // Get the metadata for this file specifically. It should only have a length of zero, nothing else.
            var dFile = (BEncodedDictionary) ((BEncodedDictionary) fileTree["D"]);
            Assert.IsTrue (dFile.ContainsKey ("a"));
            Assert.IsTrue (((BEncodedDictionary) dFile["a"]).ContainsKey ("A.txt"));

            var aFile = (BEncodedDictionary) ((BEncodedDictionary) fileTree["a"]);
            Assert.IsTrue (aFile.ContainsKey ("z"));
            Assert.IsTrue (((BEncodedDictionary) aFile["z"]).ContainsKey ("Z.txt"));


        }

        [Test]
        public void CannotCreateTorrentWithAllEmptyFiles ([Values (TorrentType.V1Only, TorrentType.V1V2Hybrid, TorrentType.V2Only)] TorrentType torrentType)
        {
            // Create a torrent from files with all zeros
            // and see if it matches the one checked into the repo.
            var factories = Factories.Default
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true, FillValue = 0 });

            var creator = new TorrentCreator (torrentType, factories) {
                PieceLength = Constants.BlockSize * 4,
            };

            var files = new List<FileMapping> { new FileMapping ("empty_source", "empty_dest", 0) };
            Assert.ThrowsAsync<InvalidOperationException> (() => creator.CreateAsync (new CustomFileSource (files)));
        }

        [Test]
        public async Task CannotLoadTorrentWithAllEmptyFiles ([Values (TorrentType.V1Only, TorrentType.V1V2Hybrid, TorrentType.V2Only)] TorrentType torrentType)
        {
            // Create a torrent from files with all zeros
            // and see if it matches the one checked into the repo.
            var factories = Factories.Default
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = true, FillValue = 0 });

            var creator = new TorrentCreator (torrentType, factories) {
                PieceLength = Constants.BlockSize * 4,
            };

            var files = new List<FileMapping> { new FileMapping ("empty_source", "empty_dest", 1) };
            var torrentDict = await creator.CreateAsync (new CustomFileSource (files));
            var infoDict = (BEncodedDictionary) torrentDict["info"];
            if (torrentType.HasV1 ()) {
                var fileDict = (infoDict["files"] as BEncodedList)[0] as BEncodedDictionary;
                fileDict["length"] = (BEncodedNumber) 0;
            }
            if (torrentType.HasV2 ()) {
                var fileDict = ((infoDict["file tree"] as BEncodedDictionary)["empty_dest"] as BEncodedDictionary)[""] as BEncodedDictionary;
                fileDict["length"] = (BEncodedNumber) 0;
            }

            Assert.Throws<InvalidOperationException> (() => Torrent.Load (torrentDict));
        }

        [Test]
        public void IllegalDestinationPath ()
        {
            Assert.Throws<ArgumentException> (() => {
                var source = new Source {
                    TorrentName = "asd",
                    Files = new[] {
                        new FileMapping("a", Path.Combine ("..", "..", "dest1"), 123)
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
