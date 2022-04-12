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

        private static async Task<Torrent> CreateTestTorrent(bool usePadding)
        {
            var creator = new TorrentCreator (Factories.Default
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = false }))
                {
                    UsePadding = usePadding
                };

            var announces = new List<List<string>> {
                new List<string> (new[] { "http://tier1.com/announce1", "http://tier1.com/announce2" }),
                new List<string> (new[] { "http://tier2.com/announce1", "http://tier2.com/announce2" })
            };

            creator.Comment = Comment;
            creator.CreatedBy = CreatedBy;
            creator.PieceLength = PieceLength;
            creator.Publisher = Publisher;
            creator.PublisherUrl = PublisherUrl;

            var files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength * 2.30)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 36.5)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir2", "File3"), (long)(PieceLength * 3.17)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir1", "File4"), (long)(PieceLength * 1.22)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir2", "File5"), (long)(PieceLength * 6.94)),
            };

            foreach (var v in announces)
                creator.Announces.Add (v);

            BEncodedDictionary dict = await creator.CreateAsync (Guid.NewGuid().ToString(), files);
            return Torrent.Load (dict);
        }

        [Test]
        public async Task Padding ()
        {
            var torrent = await CreateTestTorrent(true);

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

            // no padding on last file
            Assert.AreEqual (0, torrent.Files.Last ().Padding);
        }

        [Test]
        public async Task MD5HashNotAffectedByPadding ()
        {
            var paddedTorrent = await CreateTestTorrent (true);
            var unPaddedTorrent = await CreateTestTorrent (false);

            // whoops.. MD5 hash isn't used / published via Torrent.Files
            //foreach(var x in paddedTorrent.Files.Zip (unPaddedTorrent.Files, (paddedFile, unpaddedFile) => (paddedFile, unpaddedFile)))
            //{
            //    x.paddedFile.
            //}
        }
    }
}
