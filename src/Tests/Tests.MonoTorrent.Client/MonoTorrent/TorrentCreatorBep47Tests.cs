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

            var rawDict = await CreateTestBenc (type, files);
            var torrent = Torrent.Load (rawDict);
            Assert.AreEqual (0, torrent.Files[0].Padding);
            Assert.AreEqual (0, torrent.Files[1].Padding);
        }

        [Test]
        public async Task HybridTorrentWithEmptyFiles ()
        {
            // Hybrid torrents must be strictly alphabetically ordered so v1 and v2 metadata ends up
            // matching. These are in the wrong order.
            var inputFiles = new Source {
                TorrentName = "asfg",
                Files = new[] {
                    new FileMapping (Path.Combine("a", "File1"), Path.Combine("a", "File1"), 2),
                    new FileMapping (Path.Combine("a", "File2"), Path.Combine("a", "File2"), 0),
                    new FileMapping (Path.Combine("a", "File0"), Path.Combine("a", "File0"), 1),
                }
            };

            var rawDict = await CreateTestBenc (TorrentType.V1V2Hybrid, inputFiles);

            // Load the torrent for good measure
            Assert.DoesNotThrow (() => Torrent.Load (rawDict));

            // Validate order in the v1 data. Duplicate the underlying list first as we'll remove padding from it later.
            var filesList = (BEncodedList) ((BEncodedDictionary) rawDict["info"])["files"];

            // We should have 1 padding file - the last file is empty, so the second last file has
            // no padding either. Only the first one does.
            Assert.AreEqual (4, filesList.Count);

            var padding = (BEncodedDictionary) filesList[1];
            var path = (BEncodedList) padding["path"];
            Assert.AreEqual (".pad", ((BEncodedString) path[0]).Text);
            Assert.AreEqual (PieceLength - 1, ((BEncodedNumber) padding["length"]).Number);

            // Remove the padding, then check the order of the actual files!
            filesList.RemoveAt (1);

            for (int i = 0; i < filesList.Count; i++) {
                var dict = (BEncodedDictionary) filesList[i];
                var parts = (BEncodedList) dict["path"];
                Assert.AreEqual (2, parts.Count);
                Assert.AreEqual ("a", parts[0].ToString ());
                Assert.AreEqual ("File" + i, parts[1].ToString ());
            }

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

        [Test]
        public void HybridTorrent_FinalFileHasUnexpectedPadding ([Values(true, false)] bool hasFinalFilePadding)
        {
            // Test validating both variants of torrent can be loaded
            //
            // https://github.com/bittorrent/bittorrent.org/issues/160
            //
            var v1Files = new BEncodedList {
                new BEncodedDictionary {
                    { "length", (BEncodedNumber)9 },
                    { "path", new BEncodedList{ (BEncodedString)"file1.txt" } },
                },
                new BEncodedDictionary {
                    { "attr", (BEncodedString) "p"},
                    { "length", (BEncodedNumber)32759 },
                    { "path", new BEncodedList{ (BEncodedString)".pad32759" } },
                },

                new BEncodedDictionary {
                    { "length", (BEncodedNumber) 14 },
                    { "path", new BEncodedList{ (BEncodedString)"file2.txt" } },
                }
            };

            if (hasFinalFilePadding)
                v1Files.Add (new BEncodedDictionary {
                    { "attr", (BEncodedString) "p" },
                    { "length", (BEncodedNumber)32754 },
                    { "path", new BEncodedList{ (BEncodedString)".pad32754" } },
                });

            var v2Files = new BEncodedDictionary {
                { "file1.txt", new BEncodedDictionary {
                    {"", new BEncodedDictionary {
                        { "length" , (BEncodedNumber) 9 },
                        { "pieces root", (BEncodedString) Enumerable.Repeat<byte>(0, 32).ToArray () }
                    } }
                } },

                { "file2.txt", new BEncodedDictionary {
                    {"", new BEncodedDictionary {
                        { "length", (BEncodedNumber) 14 },
                        { "pieces root", (BEncodedString) Enumerable.Repeat<byte>(1, 32).ToArray () }
                    } }
                } },
            };

            var infoDict = new BEncodedDictionary {
                {"files", v1Files },
                {"file tree", v2Files },
                { "meta version", (BEncodedNumber) 2 },
                { "name", (BEncodedString) "padding test"},
                { "piece length", (BEncodedNumber) 32768},
                { "pieces", (BEncodedString) new byte[40] }
            };

            var dict = new BEncodedDictionary {
                { "info", infoDict }
            };

            var torrent = Torrent.Load (dict);
            Assert.AreEqual (2, torrent.Files.Count);
            Assert.AreEqual (9, torrent.Files[0].Length);
            Assert.AreEqual (32768 - 9, torrent.Files[0].Padding);
            Assert.AreEqual (14, torrent.Files[1].Length);
            Assert.AreEqual (hasFinalFilePadding ? 32768 - 14 : 0, torrent.Files[1].Padding);
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
