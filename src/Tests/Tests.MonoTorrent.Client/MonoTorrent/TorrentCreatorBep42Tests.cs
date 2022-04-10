using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.PieceWriter;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentCreatorBep42Tests
    {
        const string Comment = "My Comment";
        const string CreatedBy = "Created By MonoTorrent";
        const long PieceLength = 64 * 1024;
        const string Publisher = "My Publisher";
        const string PublisherUrl = "www.mypublisher.com";

        List<List<string>> announces;
        TorrentCreator creator;
        List<TorrentCreator.InputFile> files;

        Factories TestFactories => Factories.Default
            .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = false });

        [SetUp]
        public void Setup ()
        {
            creator = new TorrentCreator (TestFactories) { UsePadding = true };
            announces = new List<List<string>> {
                new List<string> (new[] { "http://tier1.com/announce1", "http://tier1.com/announce2" }),
                new List<string> (new[] { "http://tier2.com/announce1", "http://tier2.com/announce2" })
            };

            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;

            files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength * 2.30)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 36.5)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir2", "File3"), (long)(PieceLength * 3.17)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir1", "File4"), (long)(PieceLength * 1.22)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir2", "File5"), (long)(PieceLength * 6.94)),
            };
        }

        [Test]
        public async Task CreateMultiTest ()
        {
            foreach (var v in announces)
                creator.Announces.Add (v);

            BEncodedDictionary dict = await creator.CreateAsync ("TorrentName", files);
            Torrent torrent = Torrent.Load (dict);

            Assert.AreEqual (5, torrent.Files.Count);

            // all files start exactly at piece boundaries
            foreach (var f in torrent.Files)
            {
                Assert.IsTrue (f.OffsetInTorrent % torrent.PieceLength == 0);
            }

            // padded file lengths (except the last) are multiples of piecelength
            foreach (var f in torrent.Files.Take(torrent.Files.Count-1)) {
                Assert.IsTrue ((f.Length + f.Padding) % torrent.PieceLength == 0);
            }
        }
    }
}
