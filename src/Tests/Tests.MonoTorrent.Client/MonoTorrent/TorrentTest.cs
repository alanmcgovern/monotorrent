//
// TorrentTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.IO;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentTest
    {
        BEncodedDictionary torrentInfo;
        private Torrent torrent;
        private long creationTime;
        readonly System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create ();

        /// <summary>
        ///
        /// </summary>
        [SetUp]
        public void StartUp ()
        {
            DateTime current = new DateTime (2006, 7, 1, 5, 5, 5);
            DateTime epochStart = new DateTime (1970, 1, 1, 0, 0, 0);
            TimeSpan span = current - epochStart;
            creationTime = (long) span.TotalSeconds;

            torrentInfo = new BEncodedDictionary {
                { "announce", new BEncodedString ("http://myannouceurl/announce") },
                { "creation date", new BEncodedNumber (creationTime) },
                { "nodes", new BEncodedList () },                    //FIXME: What is this?
                { "comment.utf-8", new BEncodedString ("my big long comment") },
                { "comment", new BEncodedString ("my big long comment") },
                { "azureus_properties", new BEncodedDictionary () }, //FIXME: What is this?
                { "created by", new BEncodedString ($"MonoTorrent/{GitInfoHelper.ClientVersion}") },
                { "encoding", new BEncodedString ("UTF-8") },
                { "info", CreateInfoDict () },
                { "private", new BEncodedString ("1") },
                { "url-list", new BEncodedList() {
                    new BEncodedString ("https://example.com/8/items/"),
                    new BEncodedString ("/8/items/"), // this should be ignored on loading
                } }
            };
            torrent = Torrent.Load (torrentInfo);

            // People using monotorrent should only see V1 torrent infohashes (for now).
            Assert.IsNotNull (torrent.InfoHashes.V1);
            Assert.IsNull (torrent.InfoHashes.V2);
        }
        private BEncodedDictionary CreateInfoDict ()
        {
            BEncodedDictionary dict = new BEncodedDictionary {
                { "source", new BEncodedString ("http://www.thisiswhohostedit.com") },
                { "sha1", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("this is a sha1 hash string"))) },
                { "ed2k", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("ed2k isn't a sha, but who cares"))) },
                { "publisher-url.utf-8", new BEncodedString ("http://www.iamthepublisher.com") },
                { "publisher-url", new BEncodedString ("http://www.iamthepublisher.com") },
                { "publisher.utf-8", new BEncodedString ("MonoTorrent Inc.") },
                { "publisher", new BEncodedString ("MonoTorrent Inc.") },
                { "files", CreateFiles () },
                { "name.utf-8", new BEncodedString ("MyBaseFolder") },
                { "name", new BEncodedString ("MyBaseFolder") },
                { "piece length", new BEncodedNumber (512) },
                { "private", new BEncodedString ("1") },
                { "pieces", new BEncodedString (new byte [((26000 + 512) / 512) * 20]) } // Total size is 26000, piecelength is 512
            };
            return dict;
        }
        private BEncodedList CreateFiles ()
        {
            BEncodedList files = new BEncodedList ();

            BEncodedList path = new BEncodedList {
                new BEncodedString (""),
                new BEncodedString ("file1.txt")
            };

            BEncodedDictionary file = new BEncodedDictionary {
                { "sha1", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash1"))) },
                { "ed2k", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash2"))) },
                { "length", new BEncodedNumber (50000) },
                { "md5sum", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash3"))) },
                { "path.utf-8", path },
                { "path", path }
            };

            files.Add (file);


            path = new BEncodedList {
                new BEncodedString ("subfolder1"),
                new BEncodedString ("file2.txt")
            };

            file = new BEncodedDictionary {
                { "sha1", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash1"))) },
                { "ed2k", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash2"))) },
                { "length", new BEncodedNumber (60000) },
                { "md5sum", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash3"))) },
                { "path.utf-8", path },
                { "path", path }
            };

            files.Add (file);


            path = new BEncodedList {
                new BEncodedString ("subfolder1"),
                new BEncodedString ("subfolder2"),
                new BEncodedString ("file3.txt")
            };

            file = new BEncodedDictionary {
                { "sha1", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash1"))) },
                { "ed2k", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash2"))) },
                { "length", new BEncodedNumber (70000) },
                { "md5sum", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash3"))) },
                { "path.utf-8", path },
                { "path", path }
            };

            files.Add (file);


            path = new BEncodedList {
                new BEncodedString ("subfolder1"),
                new BEncodedString ("subfolder2"),
                new BEncodedString ("file4.txt")
            };

            file = new BEncodedDictionary {
                { "sha1", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash1"))) },
                { "ed2k", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash2"))) },
                { "length", new BEncodedNumber (80000) },
                { "md5sum", new BEncodedString (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("file1 hash3"))) },
                { "path.utf-8", path },
                { "path", path }
            };

            files.Add (file);

            return files;
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void AnnounceUrl ()
        {
            Assert.IsTrue (torrent.AnnounceUrls.Count == 1);
            Assert.IsTrue (torrent.AnnounceUrls[0].Count == 1);
            Assert.IsTrue (torrent.AnnounceUrls[0][0] == "http://myannouceurl/announce");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void CreationDate ()
        {
            Assert.AreEqual (2006, torrent.CreationDate.Year, "Year wrong");
            Assert.AreEqual (7, torrent.CreationDate.Month, "Month Wrong");
            Assert.AreEqual (1, torrent.CreationDate.Day, "Day Wrong");
            Assert.AreEqual (5, torrent.CreationDate.Hour, "Hour Wrong");
            Assert.AreEqual (5, torrent.CreationDate.Minute, "Minute Wrong");
            Assert.AreEqual (5, torrent.CreationDate.Second, "Second Wrong");
            Assert.AreEqual (new DateTime (2006, 7, 1, 5, 5, 5), torrent.CreationDate);
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Comment ()
        {
            Assert.AreEqual (torrent.Comment, "my big long comment");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void CreatedBy ()
        {
            Assert.AreEqual (torrent.CreatedBy, $"MonoTorrent/{GitInfoHelper.ClientVersion}");
        }

        [Test]
        public void NodesIsNotAList ()
        {
            torrentInfo["nodes"] = new BEncodedString ("192.168.0.1:12345");
            torrent = Torrent.Load (torrentInfo);
            Assert.AreEqual (0, torrent.Nodes.Count, "#1");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void ED2K ()
        {
            Assert.IsTrue (torrent.ED2K.Span.SequenceEqual (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("ed2k isn't a sha, but who cares"))));
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Encoding ()
        {
            Assert.IsTrue (torrent.Encoding == "UTF-8");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Files ()
        {
            Assert.AreEqual (4, torrent.Files.Count);

            Assert.AreEqual ("file1.txt", torrent.Files[0].Path);
            Assert.AreEqual (50000, torrent.Files[0].Length);

            Assert.AreEqual (Path.Combine ("subfolder1", "file2.txt"), torrent.Files[1].Path);
            Assert.AreEqual (60000, torrent.Files[1].Length);

            Assert.AreEqual (Path.Combine (Path.Combine ("subfolder1", "subfolder2"), "file3.txt"), torrent.Files[2].Path);
            Assert.AreEqual (70000, torrent.Files[2].Length);

            Assert.AreEqual (Path.Combine (Path.Combine ("subfolder1", "subfolder2"), "file4.txt"), torrent.Files[3].Path);
            Assert.AreEqual (80000, torrent.Files[3].Length);
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void HttpSeeds ()
        {
            Assert.IsTrue (torrent.HttpSeeds.Count == 1);
            Assert.AreEqual (new Uri ("https://example.com/8/items/"), torrent.HttpSeeds[0]);
        }

        [Test]
        public void InvalidPath ()
        {
            var files = ((BEncodedDictionary) torrentInfo["info"])["files"] as BEncodedList;

            var newFile = new BEncodedDictionary ();
            var path = new BEncodedList (new BEncodedString[] { "test", "..", "bar" });
            newFile["path"] = path;
            newFile["length"] = (BEncodedNumber) 15251;
            files.Add (newFile);

            Assert.Throws<ArgumentException> (() => Torrent.Load (torrentInfo));
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Name ()
        {
            Assert.IsTrue (torrent.Name == "MyBaseFolder");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Private ()
        {
            Assert.AreEqual (true, torrent.IsPrivate);
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void PublisherUrl ()
        {
            Assert.AreEqual ("http://www.iamthepublisher.com", torrent.PublisherUrl);
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void PieceLength ()
        {
            Assert.IsTrue (torrent.PieceLength == 512);
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Publisher ()
        {
            Assert.IsTrue (torrent.Publisher == "MonoTorrent Inc.");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Size ()
        {
            Assert.AreEqual ((50000 + 60000 + 70000 + 80000), torrent.Size);
        }

        [Test]
        public void StartEndIndices ()
        {
            int pieceLength = 32 * 32;
            ITorrentFile[] files = TorrentFile.Create (pieceLength,
                ("File0", 0),
                ("File1", pieceLength),
                ("File2", 0),
                ("File3", pieceLength - 1),
                ("File4", 1),
                ("File5", 236),
                ("File6", pieceLength * 7)
            );
            Torrent t = TestRig.CreateMultiFileTorrent (files, pieceLength);

            Assert.AreEqual (0, t.Files[0].StartPieceIndex, "#0a");
            Assert.AreEqual (0, t.Files[0].EndPieceIndex, "#0b");

            Assert.AreEqual (0, t.Files[1].StartPieceIndex, "#1");
            Assert.AreEqual (0, t.Files[1].EndPieceIndex, "#2");

            Assert.AreEqual (0, t.Files[2].StartPieceIndex, "#3");
            Assert.AreEqual (0, t.Files[2].EndPieceIndex, "#4");

            Assert.AreEqual (1, t.Files[3].StartPieceIndex, "#5");
            Assert.AreEqual (1, t.Files[3].EndPieceIndex, "#6");

            Assert.AreEqual (1, t.Files[4].StartPieceIndex, "#7");
            Assert.AreEqual (1, t.Files[4].EndPieceIndex, "#8");

            Assert.AreEqual (2, t.Files[5].StartPieceIndex, "#9");
            Assert.AreEqual (2, t.Files[5].EndPieceIndex, "#10");

            Assert.AreEqual (2, t.Files[6].StartPieceIndex, "#11");
            Assert.AreEqual (9, t.Files[6].EndPieceIndex, "#12");
        }

        [Test]
        public void StartEndIndices2 ()
        {
            int pieceLength = 32 * 32;
            ITorrentFile[] files = TorrentFile.Create (pieceLength,
                ("File0", pieceLength),
                ("File1", 0)
            );
            Torrent t = TestRig.CreateMultiFileTorrent (files, pieceLength);

            Assert.AreEqual (0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual (0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual (0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual (0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices3 ()
        {
            int pieceLength = 32 * 32;
            ITorrentFile[] files = TorrentFile.Create (pieceLength,
                ("File0", pieceLength - 10),
                ("File1", 10)
            );
            Torrent t = TestRig.CreateMultiFileTorrent (files, pieceLength);

            Assert.AreEqual (0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual (0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual (0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual (0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices4 ()
        {
            int pieceLength = 32 * 32;
            ITorrentFile[] files = TorrentFile.Create (pieceLength,
                ("File0", pieceLength - 10),
                ("File1", 11)
            );
            Torrent t = TestRig.CreateMultiFileTorrent (files, pieceLength);

            Assert.AreEqual (0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual (0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual (0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual (1, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices5 ()
        {
            int pieceLength = 32 * 32;
            ITorrentFile[] files = TorrentFile.Create (pieceLength,
                ("File0", pieceLength - 10),
                ("File1", 10),
                ("File1", 1)
            );
            Torrent t = TestRig.CreateMultiFileTorrent (files, pieceLength);

            Assert.AreEqual (0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual (0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual (0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual (0, t.Files[1].EndPieceIndex, "#4");

            Assert.AreEqual (1, t.Files[2].StartPieceIndex, "#5");
            Assert.AreEqual (1, t.Files[2].EndPieceIndex, "#6");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void Source ()
        {
            Assert.IsTrue (torrent.Source == "http://www.thisiswhohostedit.com");
        }

        /// <summary>
        ///
        /// </summary>
        [Test]
        public void SHA1 ()
        {
            Assert.IsTrue (torrent.SHA1.Span.SequenceEqual (sha.ComputeHash (System.Text.Encoding.UTF8.GetBytes ("this is a sha1 hash string"))));
        }

        [Test]
        public void V1InfoHashOnly ()
        {
            Assert.IsNull (Torrent.Load (torrentInfo).InfoHashes.V2);
        }

        [Test]
        public void EmptyCreationDate ()
        {
            var info = torrentInfo;
            info.Remove ("creation date");
            info.Add ("creation date", new BEncodedString (String.Empty));

            Assert.DoesNotThrow (() => Torrent.Load (torrentInfo));
        }
    }
}
