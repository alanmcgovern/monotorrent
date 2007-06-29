//
// TorrentCreatorSingleFileTest.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
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
using NUnit.Framework;

using MonoTorrent.Common;

namespace MonoTorrent.Common.Test
{
    
    [TestFixture]
    public class TorrentCreatorSingleFileTest
    {       
    
        private const string TORRENT_PATH="single.torrent";
        private const string COMMENT = "a comment";
        private const string ANNOUNCE_URL = "http://127.0.0.1:10000/announce";
        private const string CREATED_BY = "monotorrent";
        private const int PIECE_LENGTH = 2 << 11;
        private const string TEST_FILE = "reallybigfile.dat";
        
        Torrent fromTorrentCreator = new Torrent();
        private long sizeReported; //lame hack
        
        public string TestTorrentPath {        
            get {
                string path = GetType().Assembly.Location;
                path = path.Remove(path.LastIndexOf(Path.DirectorySeparatorChar));               
                return Path.Combine(path, TEST_FILE);
            }
        }
        
        [SetUp]
        public void Setup()
        {
            using (FileStream fstream = new FileStream(TestTorrentPath, FileMode.Create, FileAccess.Write, FileShare.Write)) {
            
                using (StreamWriter stream = new StreamWriter(fstream)) {
                                
                    for (int i = 0; i < 10000; i++) {
                        stream.Write("test 1,2 one two one two--------");
                    }
                    stream.Close();
                }
                fstream.Close();
            }            
            
            Console.WriteLine("opened");
            
            TorrentCreator creator = new TorrentCreator();
            creator.Announces.Add(new System.Collections.Generic.List<string>());
            creator.Announces[0].Add(ANNOUNCE_URL);
            creator.Comment = COMMENT;
            creator.CreatedBy = CREATED_BY;
            creator.PieceLength = PIECE_LENGTH;
            creator.Path = TestTorrentPath;
//            creator.StoreUTF8 = true;
//            creator.StoreMD5 = true;
            creator.Create(TORRENT_PATH);
            sizeReported = creator.GetSize();
            
            fromTorrentCreator.LoadTorrent(TORRENT_PATH);
        }
        
        [Test]
        public void Name()
        {
            Assert.AreEqual(fromTorrentCreator.Files[0].Path, TEST_FILE);            
        }
        
        [Test]
        public void Length()
        {
            FileInfo info = new FileInfo(TEST_FILE);
            Assert.AreEqual(fromTorrentCreator.Files[0].Length, info.Length);
        }
        
        [Test]
        public void FileCound()
        {
            Assert.AreEqual(fromTorrentCreator.Files.Length, 1);
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
        public void TestSizeCalc()
        {
            FileInfo info = new FileInfo(TORRENT_PATH);
            Assert.AreEqual(info.Length, sizeReported, "creator calculates wrong size");
        }
        
        //[TearDown]
        public void TearDown()
        {
            File.Delete(TORRENT_PATH);
            File.Delete(TEST_FILE);
        }
        
    }
}
