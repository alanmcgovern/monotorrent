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
using System.Security.Cryptography;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentCreatorBep47Tests
    {
        const string Comment = "My Comment";
        const string CreatedBy = "Created By MonoTorrent";
        const int PieceLength = 64 * 1024;
        const string Publisher = "My Publisher";
        const string PublisherUrl = "www.mypublisher.com";

        class Source : ITorrentFileSource
        {
            public IEnumerable<FileMapping> Files { get; set; }
            public string TorrentName { get; set; }
        }

        private static async Task<BEncodedDictionary> CreateTestBenc (TorrentType type)
        {
            var files = new Source {
                TorrentName = "asd",
                Files = new[] {
                    new FileMapping(Path.Combine("Dir1", "SDir1", "File1"), Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength * 2.30)),
                    new FileMapping(Path.Combine("Dir1", "SDir1", "File2"), Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 36.5)),
                    new FileMapping(Path.Combine("Dir1", "SDir2", "File3"), Path.Combine("Dir1", "SDir2", "File3"), (long)(PieceLength * 3.17)),
                    new FileMapping(Path.Combine("Dir2", "SDir1", "File4"), Path.Combine("Dir2", "SDir1", "File4"), (long)(PieceLength * 1.22)),
                    new FileMapping(Path.Combine("Dir2", "SDir2", "File5"), Path.Combine("Dir2", "SDir2", "File5"), (long)(PieceLength * 6.94))
                }
            };
            return await CreateTestBenc (type, files);
        }

        private static async Task<BEncodedDictionary> CreateTestBenc (TorrentType type, ITorrentFileSource files)
        {
            var creator = new TorrentCreator (type, Factories.Default
                            .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = false, FillValue = 0 })) {
                StoreMD5 = true,
                StoreSHA1 = true,
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

        private static async Task<Torrent> CreateTestTorrent (TorrentType type)
        {
            return Torrent.Load (await CreateTestBenc (type));
        }

        [Test]
        [TestCase(TorrentType.V1Only)]
        [TestCase(TorrentType.V1OnlyWithPaddingFiles)]
        [TestCase(TorrentType.V1V2Hybrid)]
        [TestCase(TorrentType.V2Only)]
        public async Task CreateWithUnusualFilenames (TorrentType type)
        {
            var files = new Source {
                TorrentName = "asd",
                Files = new[] {
                    new FileMapping("ASDkfsdjgsdSDFGsj.asd", "ASDkfsdjgsdSDFGsj.asd", (long)(PieceLength * 2.30)),
                    new FileMapping("aSvzxkaSqp AZXDCaj asdASDjas ASDl.aaaaaa", "aSvzxkaSqp AZXDCaj asdASDjas ASDl.aaaaaa", (long)(PieceLength * 36.5)),
                    new FileMapping("[aaaaa aaaaaaaaa]aaaaaa a aaaaaaaa.aaaaa", "[aaaaa aaaaaaaaa]aaaaaa a aaaaaaaa.aaaaa", (long)(PieceLength * 3.17)),
                }
            };
            var torrent = await CreateTestBenc (type, files);
            Assert.DoesNotThrow (() => Torrent.Load (torrent));
        }

        [Test]
        [TestCase (TorrentType.V1Only)]
        [TestCase (TorrentType.V1OnlyWithPaddingFiles)]
        [TestCase (TorrentType.V1V2Hybrid)]
        [TestCase (TorrentType.V2Only)]
        public async Task CreateWithManyEmptyFiles (TorrentType type)
        {
            var files = new Source {
                TorrentName = "asd",
                Files = new[] {
                    new FileMapping("a", "a", 0),
                    new FileMapping("b", "b", PieceLength),
                    new FileMapping("c", "c", 0),
                    new FileMapping("d1", "d1", PieceLength / 2),
                    new FileMapping("d2", "d2", PieceLength / 2),
                    new FileMapping("e", "e", 0),
                    new FileMapping("f", "f", 0),
                    new FileMapping("g", "g", PieceLength + 1),
                    new FileMapping("h", "h", 0),
                }
            };

            var torrent = Torrent.Load (await CreateTestBenc (type, files));
            foreach(var emptyFile in files.Files.Where (t => t.Length == 0)) {
                var file = torrent.Files.Single (t => t.Path == emptyFile.Destination);
                Assert.AreEqual (0, file.Length);
                Assert.AreEqual (0, file.OffsetInTorrent);
                Assert.AreEqual (0, file.StartPieceIndex);
                Assert.AreEqual (0, file.EndPieceIndex);
                Assert.AreEqual (0, file.Padding);
                Assert.IsTrue (file.PiecesRoot.IsEmpty);
            }
            var expectedPadding = type == TorrentType.V1OnlyWithPaddingFiles || type == TorrentType.V1V2Hybrid ? PieceLength / 2 : 0;
            Assert.AreEqual (0, torrent.Files.Single (t => t.Path == "b").Padding);
            Assert.AreEqual (expectedPadding, torrent.Files.Single (t => t.Path == "d1").Padding);
            Assert.AreEqual (expectedPadding, torrent.Files.Single (t => t.Path == "d2").Padding);
            Assert.AreEqual (0, torrent.Files.Single (t => t.Path == "g").Padding);
        }

        [Test]
        public async Task FileLengthSameAsPieceLength ([Values (TorrentType.V1Only, TorrentType.V1V2Hybrid)] TorrentType type)
        {
            var files = new Source {
                TorrentName = "asfg",
                Files = new[] {
                    new FileMapping (Path.Combine("Dir1", "SDir1", "File1"), Path.Combine("Dir1", "SDir1", "File1"), (long)(PieceLength)),
                    new FileMapping (Path.Combine("Dir1", "SDir1", "File2"), Path.Combine("Dir1", "SDir1", "File2"), (long)(PieceLength * 2)),
                }
            };

            var torrent = Torrent.Load (await CreateTestBenc (type, files));
            Assert.AreEqual (0, torrent.Files[0].Padding);
            Assert.AreEqual (0, torrent.Files[1].Padding);
        }

        [Test]
        public async Task Padding ([Values(TorrentType.V1OnlyWithPaddingFiles, TorrentType.V1V2Hybrid)] TorrentType type)
        {
            var torrent = await CreateTestTorrent (type);

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
            var paddedBenc = await CreateTestBenc (TorrentType.V1OnlyWithPaddingFiles);
            var unPaddedBenc = await CreateTestBenc (TorrentType.V1Only);

            var infoA = (BEncodedDictionary) paddedBenc[(BEncodedString) "info"];
            var filesA = (BEncodedList) infoA[(BEncodedString) "files"];

            var infoB = (BEncodedDictionary) unPaddedBenc[(BEncodedString) "info"];
            var filesB = (BEncodedList) infoB[(BEncodedString) "files"];

            for (int i = 0; i < filesA.Count; i++) {
                var fileA = (BEncodedDictionary) filesA[0];
                long lengthA = ((BEncodedNumber) fileA[(BEncodedString) "length"]).Number;
                var md5sumA = ((BEncodedString) fileA[(BEncodedString) "md5sum"]);

                var fileB = (BEncodedDictionary) filesB[0];
                long lengthB = ((BEncodedNumber) fileB[(BEncodedString) "length"]).Number;
                var md5sumB = ((BEncodedString) fileB[(BEncodedString) "md5sum"]);

                Assert.AreEqual (lengthA, lengthB);
                Assert.AreEqual (md5sumA, md5sumB);

                Assert.AreEqual (MD5SumZeros (lengthA), md5sumA);
            }
        }

        [Test]
        public async Task SHA1HashNotAffectedByPadding ()
        {
            var paddedBenc = await CreateTestBenc (TorrentType.V1OnlyWithPaddingFiles);

            var infoA = (BEncodedDictionary) paddedBenc[(BEncodedString) "info"];
            var filesA = (BEncodedList) infoA[(BEncodedString) "files"];

            for (int i = 0; i < filesA.Count; i++) {
                var fileA = (BEncodedDictionary) filesA[0];
                long lengthA = ((BEncodedNumber) fileA[(BEncodedString) "length"]).Number;
                var sha1sumA = ((BEncodedString) fileA[(BEncodedString) "sha1"]);

                Assert.AreEqual (SHA1SumZeros (lengthA), sha1sumA);
            }
        }

        [Test]
        public async Task HybridTorrentWithPadding ()
        {
            var unpaddedFile = new byte[] { 1 };
            var paddedFile = new byte[Constants.BlockSize * 2];
            paddedFile[0] = 1;

            var files = new Source {
                TorrentName = "asfg",
                Files = new[] {
                    new FileMapping ("1.src", "1.tmp", unpaddedFile.Length),
                    new FileMapping ("2.tmp", "2.tmp", unpaddedFile.Length),
                }
            };


            var creator = new TorrentCreator (TorrentType.V1V2Hybrid, Factories.Default
                .WithPieceWriterCreator (maxOpenFiles => new TestWriter { DontWrite = false, FillValue = 1 })) {
                PieceLength = paddedFile.Length
            };

            // First one's padded
            var firstSHA1 = SHA1.Create ().ComputeHash (paddedFile);
            // Second piece is *not* padded
            var secondSHA1 = SHA1.Create ().ComputeHash (unpaddedFile);

            // but bittorrent v2 hashes don't use padding
            var sha256 = SHA256.Create ().ComputeHash (unpaddedFile);

            var torrent = Torrent.Load (await creator.CreateAsync (files));
            Assert.IsTrue (firstSHA1.AsSpan ().SequenceEqual (torrent.CreatePieceHashes ().GetHash (0).V1Hash.Span));
            Assert.IsTrue (secondSHA1.AsSpan ().SequenceEqual (torrent.CreatePieceHashes ().GetHash (1).V1Hash.Span));

            Assert.IsTrue (sha256.AsSpan ().SequenceEqual (torrent.CreatePieceHashes ().GetHash (0).V2Hash.Span));
            Assert.IsTrue (sha256.AsSpan ().SequenceEqual (torrent.CreatePieceHashes ().GetHash (1).V2Hash.Span));
        }

        static BEncodedString SHA1SumZeros (long length)
        {
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA1);
            Span<byte> zeros = stackalloc byte[256];
            zeros.Clear ();
            while(length > 0) {
                var buffer = zeros.Slice (0, (int) Math.Min (length, zeros.Length));
                hasher.AppendData (buffer);
                length -= buffer.Length;
            }
            return hasher.GetHashAndReset ();
        }

        static BEncodedString MD5SumZeros (long length)
        {
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.MD5);
            Span<byte> zeros = stackalloc byte[256];
            zeros.Clear ();
            while (length > 0) {
                var buffer = zeros.Slice (0, (int) Math.Min (length, zeros.Length));
                hasher.AppendData (buffer);
                length -= buffer.Length;
            }
            return hasher.GetHashAndReset ();
        }
    }
}
