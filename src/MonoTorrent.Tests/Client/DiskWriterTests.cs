//
// DiskWriterTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;
using NUnit.Framework;
using System.IO;

namespace MonoTorrent.Client
{
    public class FakeDiskWriter : DiskWriter
    {
        public string CreateFilePath(TorrentFile file, string path)
        {
            return base.GenerateFilePath(path, file);
        }
    }
    
    [TestFixture]
    public class DiskWriterTests
    {
        [Test]
        public void SameFilePath()
        {
            // Simulate two torrents each containing a file at "Folder/File"
            TorrentFile f1 = new TorrentFile("Folder/File", 12345);
            TorrentFile f2 = new TorrentFile("Folder/File", 54321);

            FakeDiskWriter writer = new FakeDiskWriter();
            string s1 = writer.CreateFilePath(f1, "Root1");
            string s2 = writer.CreateFilePath(f1, "Root2");

            Assert.AreNotEqual(s1, s2, "#1");
            Assert.AreEqual(Path.Combine("Root1", f1.Path), s1, "#2");
            Assert.AreEqual(Path.Combine("Root2", f2.Path), s2, "#3");
        }
    }
}
