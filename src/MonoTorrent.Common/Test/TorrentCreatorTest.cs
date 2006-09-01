//
// System.String.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
using System.Text;
using System.Diagnostics;


using NUnit.Framework;

namespace MonoTorrent.Common.Test
{
    [TestFixture]
    public class TorrentCreatorTest 
    {
        private const string TORRENT_PATH="torrentcreator.torrent";
        private const string COMMENT = "a comment";
        private const string ANNOUNCE_URL = "http://127.0.0.1:10000/announce";
        private const string CREATED_BY = "monotorrent";
        private const int PIECE_LENGTH = 2 << 11;        
        Torrent fromTorrentCreator = new Torrent();
        
        private int sizeReported; //lame hack
        
        public string TestTorrentPath {
            get {
                string path = GetType().Assembly.Location;
                path = path.Remove(path.LastIndexOf(Path.DirectorySeparatorChar));
                path += Path.DirectorySeparatorChar + "testtorrent";
                return path;
            }
        }
        
        private void SetUpTorrentTree()
        {
            Directory.CreateDirectory(TestTorrentPath);
            Directory.CreateDirectory(TestTorrentPath + Path.DirectorySeparatorChar + "test");
            Directory.CreateDirectory(TestTorrentPath + Path.DirectorySeparatorChar + "test01");
            Directory.CreateDirectory(TestTorrentPath + Path.DirectorySeparatorChar + "test02");
                        
            string smallContents = "test 12 ahaha 1212 ah 12";                        
            WriteInFile(TestTorrentPath + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "file.txt",  smallContents);
            WriteInFile(TestTorrentPath + Path.DirectorySeparatorChar + "test01" + Path.DirectorySeparatorChar + "file.txt",  smallContents);
            
            StringBuilder bigContents = new StringBuilder();
            for (int i = 0; i < 2000; i++) {
                bigContents.Append(smallContents);
                bigContents.Append("\n");
            }
            
            WriteInFile(TestTorrentPath + Path.DirectorySeparatorChar + "test02" + Path.DirectorySeparatorChar + "file.txt",  bigContents.ToString());
        }
                
        private void WriteInFile(string path, string contents)
        {
            using (StreamWriter stream = new StreamWriter(path)) {            
                stream.Write(contents);
            }
        }
        
        [SetUp]
        public void CreateMultiTorrent()
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            SetUpTorrentTree();
            TorrentCreator creator = new TorrentCreator();
            creator.AddAnnounce(ANNOUNCE_URL);
            creator.Comment = COMMENT;
            creator.CreatedBy = CREATED_BY;
            creator.PieceLength = PIECE_LENGTH;
            creator.Path = TestTorrentPath;//path starting in MonoTorrent/bin/Debug
//            creator.StoreUTF8 = true;
//            creator.StoreMD5 = true;
            creator.StoreCreationDate = true;
            
            creator.Create(TORRENT_PATH);
            sizeReported = creator.GetSize();
            
            
            fromTorrentCreator.LoadTorrent(TORRENT_PATH);
        }
        
        [Test]
        public void CreatedBy()
        {
            Assert.AreEqual(CREATED_BY, fromTorrentCreator.CreatedBy, "created by wrong set");
        }
        
        [Test]
        public void Comment() 
        {
            Console.WriteLine(fromTorrentCreator.Comment);
            Assert.AreEqual(COMMENT, fromTorrentCreator.Comment, "comment wrong set");
        }        
            
        [Test]
        public void AnnounceURL() 
        {            
            Assert.AreEqual(ANNOUNCE_URL, fromTorrentCreator.AnnounceUrls[0], "announce url wrong");
        }
        
        [Test]
        public void CountFiles()
        {
            Assert.AreEqual(fromTorrentCreator.Files.Length, 3, "file count wrong");
        }
        
        [Test]
        public void File1() 
        {
            ITorrentFile creatorFile = fromTorrentCreator.Files[0];
            FileInfo info = new FileInfo(TestTorrentPath + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "file.txt");
                
            Assert.AreEqual(creatorFile.Length, info.Length , "length wrong");
            Assert.AreEqual(creatorFile.Path, "test" + Path.DirectorySeparatorChar + "file.txt", "path wrong");
                        
        }
        
        [Test]
        public void File2() 
        {            
            ITorrentFile creatorFile = fromTorrentCreator.Files[1];
            FileInfo info = new FileInfo(TestTorrentPath + Path.DirectorySeparatorChar + "test01" + Path.DirectorySeparatorChar + "file.txt");
                
            Assert.AreEqual(creatorFile.Length, info.Length , "length wrong");
            Assert.AreEqual(creatorFile.Path, "test01" + Path.DirectorySeparatorChar + "file.txt", "path wrong");
        }
       
        [Test]
        public void File3() 
        {            
            ITorrentFile creatorFile = fromTorrentCreator.Files[2];
            FileInfo info = new FileInfo(TestTorrentPath + Path.DirectorySeparatorChar + "test02" + Path.DirectorySeparatorChar + "file.txt");
                
            Assert.AreEqual(creatorFile.Length, info.Length , "length wrong");
            Assert.AreEqual(creatorFile.Path, "test02" + Path.DirectorySeparatorChar + "file.txt", "path wrong");
        }
        
        [Test]
        public void TestSizeCalc()
        {
            FileInfo info = new FileInfo(TORRENT_PATH);
            Assert.AreEqual(info.Length, sizeReported, "creator calculates wrong size");
        }
       
        [TearDown]
        public void RemoveMultiTorrent()
        {
            Console.WriteLine("deleting " + TestTorrentPath);
            //Directory.Delete(TestTorrentPath);
        }
    }
} 