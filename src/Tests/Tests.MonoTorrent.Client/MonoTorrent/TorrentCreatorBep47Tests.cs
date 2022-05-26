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
    public class TorrentCreatorBep47Tests
    {
        const string Comment = "My Comment";
        const string CreatedBy = "Created By MonoTorrent";
        const long PieceLength = 64 * 1024;
        const string Publisher = "My Publisher";
        const string PublisherUrl = "www.mypublisher.com";

        private static async Task<BEncodedDictionary> CreateTestBenc (bool usePadding)
        {
            var files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength * 2.30)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 36.5)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir2", "File3"), (long)(PieceLength * 3.17)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir1", "File4"), (long)(PieceLength * 1.22)),
                new TorrentCreator.InputFile(Path.Combine("Dir2", "SDir2", "File5"), (long)(PieceLength * 6.94)),
            };
            return await CreateTestBenc (usePadding, files);
        }

        private static async Task<BEncodedDictionary> CreateTestBenc (bool usePadding, List<TorrentCreator.InputFile> files)
        {
            var creator = new TorrentCreator (Factories.Default
                            .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = false })) {
                UsePadding = usePadding,
                StoreMD5 = true,
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

            foreach (var v in announces)
                creator.Announces.Add (v);

            return await creator.CreateAsync (Guid.Empty.ToString (), files);
        }

        private static async Task<Torrent> CreateTestTorrent (bool usePadding)
        {
            return Torrent.Load (await CreateTestBenc (usePadding));
        }

        [Test]
        public async Task FileLengthSameAsPieceLength ()
        {
            var files = new List<TorrentCreator.InputFile> {
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength)),
                new TorrentCreator.InputFile(Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 2)),
            };

            var torrent = Torrent.Load (await CreateTestBenc (true, files));
            Assert.AreEqual (0, torrent.Files[0].Padding);
            Assert.AreEqual (0, torrent.Files[1].Padding);
        }

        [Test]
        public async Task Padding ()
        {
            var torrent = await CreateTestTorrent (true);

            Assert.AreEqual (5, torrent.Files.Count);

            // all files start exactly at piece boundaries
            foreach (var f in torrent.Files) {
                Assert.IsTrue (f.OffsetInTorrent % torrent.PieceLength == 0);
            }

            // padded file lengths (except the last) are multiples of piecelength
            foreach (var f in torrent.Files.Take (torrent.Files.Count - 1)) {
                Assert.IsTrue ((f.Length + f.Padding) % torrent.PieceLength == 0);
            }

            // no padding on last file
            Assert.AreEqual (0, torrent.Files.Last ().Padding);
        }

        [Test]
        public async Task MD5HashNotAffectedByPadding ()
        {
            var paddedBenc = await CreateTestBenc (true);
            var unPaddedBenc = await CreateTestBenc (false);

            var infoA = (BEncodedDictionary) paddedBenc[(BEncodedString) "info"];
            var filesA = (BEncodedList) infoA[(BEncodedString) "files"];
            var fileA = (BEncodedDictionary) filesA[0];
            long lengthA = ((BEncodedNumber) fileA[(BEncodedString) "length"]).Number;
            var md5sumA = ((BEncodedString) fileA[(BEncodedString) "md5sum"]);

            var infoB = (BEncodedDictionary) unPaddedBenc[(BEncodedString) "info"];
            var filesB = (BEncodedList) infoB[(BEncodedString) "files"];
            var fileB = (BEncodedDictionary) filesB[0];
            long lengthB = ((BEncodedNumber) fileB[(BEncodedString) "length"]).Number;
            var md5sumB = ((BEncodedString) fileB[(BEncodedString) "md5sum"]);

            Assert.AreEqual (lengthA, lengthB);
            Assert.AreEqual (md5sumA.ToHex (), md5sumB.ToHex ());
        }
    }
}
