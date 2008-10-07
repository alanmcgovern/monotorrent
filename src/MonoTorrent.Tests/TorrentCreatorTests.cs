using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tests;
using System.IO;

namespace MonoTorrent.Common.Tests
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        //static void Main(string[] args)
        //{
        //    TorrentCreatorTests t = new TorrentCreatorTests();
        //    t.Setup();
        //    t.CreateTest();
        //}
        private string Comment = "My Comment";
        private string CreatedBy = "Created By MonoTorrent";
        private int PieceLength = 64 * 1024;
        private string Publisher = "My Publisher";
        private string PublisherUrl = "www.mypublisher.com";
        private BEncodedString CustomKey = "Custom Key";
        private BEncodedString CustomValue = "My custom value";

        private TorrentCreator creator;
        TorrentFile[] files;

        [SetUp]
        public void Setup()
        {
            creator = new TorrentCreator();
            creator.Announces.Add(new List<string>(new string[] { "http://tracker1.com/announce1", "http://tracker1.com/announce2" }));
            creator.Announces.Add(new List<string>(new string[] { "http://tracker2.com/announce1", "http://tracker2.com/announce2" }));
            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;
            creator.AddCustom(CustomKey, CustomValue);
            files = new TorrentFile[] { 
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File1"), 15 * 5136),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File2"), 42 * 2461),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir2"), "File3"), 145 * 4151),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir1"), "File4"), 262 * 835),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir2"), "File5"), 16 * 123),
            };
        }

        [Test]
        public void CreateTest()
        {
            BEncodedDictionary dict = creator.Create(files, new TestWriter(), "TorrentName");
            Torrent torrent = Torrent.Load(dict);

            Assert.AreEqual(Comment, torrent.Comment, "#1");
            Assert.AreEqual(CreatedBy, torrent.CreatedBy, "#2");
            Assert.IsTrue((DateTime.Now - torrent.CreationDate) < TimeSpan.FromSeconds(10), "#3");
            Assert.AreEqual(PieceLength, torrent.PieceLength, "#4");
            Assert.AreEqual(Publisher, torrent.Publisher, "#5");
            Assert.AreEqual(PublisherUrl, torrent.PublisherUrl, "#6");

            for (int i = 0; i < torrent.Files.Length; i++)
                Assert.IsTrue(Array.Exists<TorrentFile>(files, delegate(TorrentFile f) { return f.Equals(torrent.Files[i]); }));
        }
    }
}
