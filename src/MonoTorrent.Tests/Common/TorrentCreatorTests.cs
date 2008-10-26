using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using System.IO;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        private string Comment = "My Comment";
        private string CreatedBy = "Created By MonoTorrent";
        private int PieceLength = 64 * 1024;
        private string Publisher = "My Publisher";
        private string PublisherUrl = "www.mypublisher.com";
        private BEncodedString CustomKey = "Custom Key";
        private BEncodedString CustomValue = "My custom value";

        List<List<string>> announces;
        private TorrentCreator creator;
        TorrentFile[] files;
        TestWriter writer;

        [SetUp]
        public void Setup()
        {
            creator = new TorrentCreator();
            announces = new List<List<string>>();

            announces.Add(new List<string>(new string[] { "http://tier1.com/announce1", "http://tier1.com/announce2" }));
            announces.Add(new List<string>(new string[] { "http://tier2.com/announce1", "http://tier2.com/announce2" }));

            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;
            creator.AddCustom(CustomKey, CustomValue);
            files = new TorrentFile[] { 
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File1"), (int)(PieceLength * 2.30), 0, 1),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File2"), (int)(PieceLength * 36.5), 1, 3),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir2"), "File3"), (int)(PieceLength * 3.17), 3, 12),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir1"), "File4"), (int)(PieceLength * 1.22), 12, 15),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir2"), "File5"), (int)(PieceLength * 6.94), 15, 15),
            };

            writer = new TestWriter();
        }

        [Test]
        public void CreateMultiTest()
        {
            creator.Announces.AddRange(announces);

            BEncodedDictionary dict = creator.Create (files, new TestWriter(), "TorrentName");
            Torrent torrent = Torrent.Load(dict);
            
            VerifyCommonParts(torrent);
            for (int i = 0; i < torrent.Files.Length; i++)
                Assert.IsTrue(Array.Exists<TorrentFile>(files, delegate(TorrentFile f) { return f.Equals(torrent.Files[i]); }));
        }
        [Test]
        public void NoTrackersTest()
        {
            BEncodedDictionary dict = creator.Create(files, new TestWriter(), "TorrentName");
			Torrent t = Torrent.Load (dict);
			Assert.AreEqual (0, t.AnnounceUrls.Count, "#1");
        }
        [Test]
        public void CreateSingleTest()
        {
            creator.Announces.AddRange(announces);

            TorrentFile f = new TorrentFile(Path.GetFileName(files[0].Path),
                                            files[0].Length,
                                            files[0].StartPieceIndex,
                                            files[0].EndPieceIndex);

            BEncodedDictionary dict = creator.Create(new TorrentFile[] { f }, new TestWriter(), f.Path);
            Torrent torrent = Torrent.Load(dict);
            
            VerifyCommonParts(torrent);
            Assert.AreEqual(1, torrent.Files.Length, "#1");
            Assert.AreEqual(f, torrent.Files[0], "#2");
        }
        [Test]
        public void CreateSingleFromFolder ()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly ();
            creator.Path = Path.GetFullPath (assembly.Location);
            BEncodedDictionary dict = creator.Create ();

            Torrent t = Torrent.Load (dict);

            Assert.AreEqual (1, t.Files.Length, "#1");
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Name, "#2");
            Assert.AreEqual (Path.GetFileName (assembly.Location), t.Files[0].Path, "#3");
        }
        
        [Test]
        public void CheckPaths()
        {
            creator.Path = Environment.CurrentDirectory;
            Environment.CurrentDirectory = Path.GetPathRoot(Environment.CurrentDirectory);
            creator.Create(files, writer, "TopDir");

            Assert.AreEqual(files.Length, writer.Paths.Count, "#1");
            foreach (TorrentFile f in files)
                Assert.IsTrue(writer.Paths.Contains(Path.Combine(creator.Path, f.Path)), "#2");
        }

        [Test]
        public void CreateFromFolder()
        {
            creator.Path = Environment.CurrentDirectory;
            Environment.CurrentDirectory = Path.GetPathRoot(Environment.CurrentDirectory);
            BEncodedDictionary dict = creator.Create();
            Torrent t = Torrent.Load(dict);

            string[] parts = creator.Path.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(parts[parts.Length - 1], t.Name);

            string[] files = Directory.GetFiles(creator.Path, "*", SearchOption.AllDirectories);
            Assert.AreEqual(t.Files.Length, files.Length, "#1");
            foreach (TorrentFile f in t.Files)
                Assert.IsTrue(Array.Exists<string>(files, delegate (string s) {
                    return s.Equals (Path.Combine(creator.Path, f.Path));
                }), "#2");
        }

        void VerifyCommonParts (Torrent torrent)
        {
            Assert.AreEqual(Comment, torrent.Comment, "#1");
            Assert.AreEqual(CreatedBy, torrent.CreatedBy, "#2");
            Assert.IsTrue((DateTime.Now - torrent.CreationDate) < TimeSpan.FromSeconds(5), "#3");
            Assert.AreEqual(PieceLength, torrent.PieceLength, "#4");
            Assert.AreEqual(Publisher, torrent.Publisher, "#5");
            Assert.AreEqual(PublisherUrl, torrent.PublisherUrl, "#6");
            Assert.AreEqual(2, torrent.AnnounceUrls.Count, "#7");
            Assert.AreEqual(2, torrent.AnnounceUrls[0].Count, "#8");
            Assert.AreEqual(2, torrent.AnnounceUrls[1].Count, "#9");
        }
    }
}
