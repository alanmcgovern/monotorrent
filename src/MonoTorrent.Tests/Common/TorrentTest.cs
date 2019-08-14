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

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentTest
    {
        private Torrent torrent;
        private long creationTime;
        private System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create();

        /// <summary>
        /// 
        /// </summary>
        [SetUp]
        public void StartUp()
        {
            DateTime current = new DateTime(2006, 7, 1, 5, 5, 5);
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0);
            TimeSpan span = current - epochStart;
            creationTime = (long)span.TotalSeconds;
            Console.WriteLine(creationTime.ToString() + "Creation seconds");

            BEncodedDictionary torrentInfo = new BEncodedDictionary();
            torrentInfo.Add("announce", new BEncodedString("http://myannouceurl/announce"));
            torrentInfo.Add("creation date", new BEncodedNumber(creationTime));
            torrentInfo.Add("nodes", new BEncodedList());                    //FIXME: What is this?
            torrentInfo.Add("comment.utf-8", new BEncodedString("my big long comment"));
            torrentInfo.Add("comment", new BEncodedString("my big long comment"));
            torrentInfo.Add("azureus_properties", new BEncodedDictionary()); //FIXME: What is this?
            torrentInfo.Add("created by", new BEncodedString("MonoTorrent/" + VersionInfo.ClientVersion));
            torrentInfo.Add("encoding", new BEncodedString("UTF-8"));
            torrentInfo.Add("info", CreateInfoDict());
            torrentInfo.Add("private", new BEncodedString("1"));
            torrent = Torrent.Load(torrentInfo);
        }
        private BEncodedDictionary CreateInfoDict()
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            dict.Add("source", new BEncodedString("http://www.thisiswhohostedit.com"));
            dict.Add("sha1", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("this is a sha1 hash string"))));
            dict.Add("ed2k", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("ed2k isn't a sha, but who cares"))));
            dict.Add("publisher-url.utf-8", new BEncodedString("http://www.iamthepublisher.com"));
            dict.Add("publisher-url", new BEncodedString("http://www.iamthepublisher.com"));
            dict.Add("publisher.utf-8", new BEncodedString("MonoTorrent Inc."));
            dict.Add("publisher", new BEncodedString("MonoTorrent Inc."));
            dict.Add("files", CreateFiles());
            dict.Add("name.utf-8", new BEncodedString("MyBaseFolder"));
            dict.Add("name", new BEncodedString("MyBaseFolder"));
            dict.Add("piece length", new BEncodedNumber(512));
            dict.Add("private", new BEncodedString("1"));
            dict.Add("pieces", new BEncodedString(new byte[((26000 + 512) / 512) * 20])); // Total size is 26000, piecelength is 512
            return dict;
        }
        private BEncodedList CreateFiles()
        {
            BEncodedList files = new BEncodedList();
            BEncodedDictionary file;
            BEncodedList path;

            path = new BEncodedList();
            path.Add(new BEncodedString("file1.txt"));

            file = new BEncodedDictionary();
            file.Add("sha1", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1"))));
            file.Add("ed2k", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2"))));
            file.Add("length", new BEncodedNumber(50000));
            file.Add("md5sum", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3"))));
            file.Add("path.utf-8", path);
            file.Add("path", path);

            files.Add(file);


            path = new BEncodedList();
            path.Add(new BEncodedString("subfolder1"));
            path.Add(new BEncodedString("file2.txt"));

            file = new BEncodedDictionary();
            file.Add("sha1", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1"))));
            file.Add("ed2k", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2"))));
            file.Add("length", new BEncodedNumber(60000));
            file.Add("md5sum", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3"))));
            file.Add("path.utf-8", path);
            file.Add("path", path);

            files.Add(file);


            path = new BEncodedList();
            path.Add(new BEncodedString("subfolder1"));
            path.Add(new BEncodedString("subfolder2"));
            path.Add(new BEncodedString("file3.txt"));

            file = new BEncodedDictionary();
            file.Add("sha1", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1"))));
            file.Add("ed2k", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2"))));
            file.Add("length", new BEncodedNumber(70000));
            file.Add("md5sum", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3"))));
            file.Add("path.utf-8", path);
            file.Add("path", path);

            files.Add(file);


            path = new BEncodedList();
            path.Add(new BEncodedString("subfolder1"));
            path.Add(new BEncodedString("subfolder2"));
            path.Add(new BEncodedString("file4.txt"));

            file = new BEncodedDictionary();
            file.Add("sha1", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1"))));
            file.Add("ed2k", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2"))));
            file.Add("length", new BEncodedNumber(80000));
            file.Add("md5sum", new BEncodedString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3"))));
            file.Add("path.utf-8", path);
            file.Add("path", path);

            files.Add(file);

            return files;
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void AnnounceUrl()
        {
            Assert.IsTrue(torrent.AnnounceUrls.Count == 1);
			Assert.IsTrue(torrent.AnnounceUrls[0].Count == 1);
            Assert.IsTrue(torrent.AnnounceUrls[0][0] == "http://myannouceurl/announce");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void CreationDate()
        {
            Assert.AreEqual(2006, torrent.CreationDate.Year, "Year wrong");
            Assert.AreEqual(7, torrent.CreationDate.Month, "Month Wrong");
            Assert.AreEqual(1, torrent.CreationDate.Day, "Day Wrong");
            Assert.AreEqual(5, torrent.CreationDate.Hour, "Hour Wrong");
            Assert.AreEqual(5, torrent.CreationDate.Minute, "Minute Wrong");
            Assert.AreEqual(5, torrent.CreationDate.Second, "Second Wrong");
            Assert.AreEqual(new DateTime(2006, 7, 1, 5, 5, 5), torrent.CreationDate);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Comment()
        {
            Assert.AreEqual(torrent.Comment, "my big long comment");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void CreatedBy()
        {
            Assert.AreEqual(torrent.CreatedBy, "MonoTorrent/" + VersionInfo.ClientVersion);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void ED2K()
        {
            Assert.IsTrue(Toolbox.ByteMatch(torrent.ED2K, sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("ed2k isn't a sha, but who cares"))));
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Encoding()
        {
            Assert.IsTrue(torrent.Encoding == "UTF-8");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Files()
        {
            Assert.AreEqual(4, torrent.Files.Length);

            Assert.AreEqual("file1.txt", torrent.Files[0].Path);
            Assert.AreEqual(50000, torrent.Files[0].Length);

            Assert.AreEqual(Path.Combine("subfolder1", "file2.txt"), torrent.Files[1].Path);
            Assert.AreEqual(60000, torrent.Files[1].Length);

            Assert.AreEqual(Path.Combine(Path.Combine("subfolder1", "subfolder2"), "file3.txt"), torrent.Files[2].Path);
            Assert.AreEqual(70000, torrent.Files[2].Length);

            Assert.AreEqual(Path.Combine(Path.Combine("subfolder1", "subfolder2"), "file4.txt"), torrent.Files[3].Path);
            Assert.AreEqual(80000, torrent.Files[3].Length);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Name()
        {
            Assert.IsTrue(torrent.Name == "MyBaseFolder");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Private()
        {
            Assert.AreEqual(true, torrent.IsPrivate);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void PublisherUrl()
        {
            Assert.AreEqual("http://www.iamthepublisher.com", torrent.PublisherUrl);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void PieceLength()
        {
            Assert.IsTrue(torrent.PieceLength == 512);
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Publisher()
        {
            Assert.IsTrue(torrent.Publisher == "MonoTorrent Inc.");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Size()
        {
            Assert.AreEqual((50000 + 60000 + 70000 + 80000), torrent.Size);
        }

        [Test]
        public void StartEndIndices()
        {
            int pieceLength = 32 * 32;
            TorrentFile[] files = new TorrentFile[] {
                new TorrentFile ("File0", 0),
                new TorrentFile ("File1", pieceLength),
                new TorrentFile ("File2", 0),
                new TorrentFile ("File3", pieceLength - 1),
                new TorrentFile ("File4", 1),
                new TorrentFile ("File5", 236),
                new TorrentFile ("File6", pieceLength * 7)
            };
            Torrent t = MonoTorrent.Client.TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#0a");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#0b");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[2].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[2].EndPieceIndex, "#4");

            Assert.AreEqual(1, t.Files[3].StartPieceIndex, "#5");
            Assert.AreEqual(1, t.Files[3].EndPieceIndex, "#6");

            Assert.AreEqual(1, t.Files[4].StartPieceIndex, "#7");
            Assert.AreEqual(1, t.Files[4].EndPieceIndex, "#8");

            Assert.AreEqual(2, t.Files[5].StartPieceIndex, "#9");
            Assert.AreEqual(2, t.Files[5].EndPieceIndex, "#10");

            Assert.AreEqual(2, t.Files[6].StartPieceIndex, "#11");
            Assert.AreEqual(9, t.Files[6].EndPieceIndex, "#12");
        }

        [Test]
        public void StartEndIndices2()
        {
            int pieceLength = 32 * 32;
            TorrentFile[] files = new TorrentFile[] {
                new TorrentFile ("File0", pieceLength),
                new TorrentFile ("File1", 0)
            };
            Torrent t = MonoTorrent.Client.TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices3()
        {
            int pieceLength = 32 * 32;
            TorrentFile[] files = new TorrentFile[] {
                new TorrentFile ("File0", pieceLength- 10),
                new TorrentFile ("File1", 10)
            };
            Torrent t = MonoTorrent.Client.TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices4()
        {
            int pieceLength = 32 * 32;
            TorrentFile[] files = new TorrentFile[] {
                new TorrentFile ("File0", pieceLength- 10),
                new TorrentFile ("File1", 11)
            };
            Torrent t = MonoTorrent.Client.TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(1, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices5()
        {
            int pieceLength = 32 * 32;
            TorrentFile[] files = new TorrentFile[] {
                new TorrentFile ("File0", pieceLength- 10),
                new TorrentFile ("File1", 10),
                new TorrentFile ("File1", 1)
            };
            Torrent t = MonoTorrent.Client.TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");

            Assert.AreEqual(1, t.Files[2].StartPieceIndex, "#5");
            Assert.AreEqual(1, t.Files[2].EndPieceIndex, "#6");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void Source()
        {
            Assert.IsTrue(torrent.Source == "http://www.thisiswhohostedit.com");
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void SHA1()
        {
            Assert.IsTrue(Toolbox.ByteMatch(torrent.SHA1, sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("this is a sha1 hash string"))));
        }
    }
}