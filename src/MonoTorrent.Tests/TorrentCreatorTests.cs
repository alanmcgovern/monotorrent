using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tests;
using System.IO;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        static void Main(string[] args)
        {
            TorrentCreatorTests t = new TorrentCreatorTests();
            t.Setup();
            t.CreateSingleTest();
        }
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
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File1"), 15 * 5136, 0, 1),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File2"), 42 * 2461, 1, 3),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir2"), "File3"), 145 * 4151, 3, 12),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir1"), "File4"), 262 * 835, 12, 15),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir2"), "File5"), 16 * 123, 15, 15),
            };
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
