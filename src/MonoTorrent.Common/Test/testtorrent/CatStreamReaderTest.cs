//
// CatStreamReaderTest.cs
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
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

using MonoTorrent.Common;

namespace MonoTorrent.Common.Test
{   
    [TestFixture]
    public class CatStreamReaderTest
    {
        const string CONTENTS = "test";
        const int COUNT = 10;
        List<string> files;
        List<string> file;
        [SetUp]
        public void SetUp()
        {
            files = new List<string>();
            for (int i = 0; i < COUNT; i++) {
                string path = Path.GetTempFileName();
                files.Add(path);
                using (StreamWriter writer = File.AppendText(path)) {
                    writer.Write(CONTENTS);
                }
                //File.AppendAllText(path, CONTENTS);
            }
        
            file = new List<string>();
            file.Add(files[0]);
        }
        
        [Test]
        public void TestCat()
        {
            CatStreamReader reader = new CatStreamReader(files);
            byte[] p = new byte[CONTENTS.Length];
            int len = reader.Read(p, 0, p.Length);
            
            while (len > 0) {
                string result = Encoding.ASCII.GetString(p);
                Assert.AreEqual(result, CONTENTS);
                len = reader.Read(p, 0, p.Length);
            }
        }
        
        [Test]
        public void TestTooSmall()
        {            
            CatStreamReader reader = new CatStreamReader(file);
            byte[] p = new byte[2*CONTENTS.Length];
            int len = reader.Read(p, 0, p.Length);
            Assert.AreEqual(len, CONTENTS.Length, "length of contents");
            string result = Encoding.ASCII.GetString(p, 0, len);
            Assert.AreEqual(result, CONTENTS, "contents");
        }
        
        [Test]
        public void TestTooBig()
        {
            CatStreamReader reader = new CatStreamReader(file);
            byte[] p = new byte[CONTENTS.Length / 2];
            int len = reader.Read(p, 0, p.Length);
            Assert.AreEqual(len, p.Length, "length of contents");
            string result = Encoding.ASCII.GetString(p, 0, len);
            Assert.AreEqual(result, CONTENTS.Substring(0, p.Length), "contents");
        }
        
        [Test]
        public void OneAndAHalf()
        {
            string result = CONTENTS + CONTENTS.Substring(0, CONTENTS.Length/2);
            CatStreamReader reader = new CatStreamReader(files);
            byte[] p = new byte[CONTENTS.Length + CONTENTS.Length/2];
            int len = reader.Read(p, 0, p.Length);
            Assert.AreEqual(len, p.Length);
            Assert.AreEqual(result, Encoding.ASCII.GetString(p, 0, len));
        }
        
        public void TestFull()
        {
            StringBuilder sb = new StringBuilder(CONTENTS.Length * COUNT);
            for (int i = 0; i < COUNT; i++) {
                sb.Append(CONTENTS);
            }
            byte[] p = new byte[CONTENTS.Length * COUNT];
            
            CatStreamReader reader = new CatStreamReader(files);
            int len = reader.Read(p, 0, p.Length);
            Assert.AreEqual(sb.ToString(), Encoding.ASCII.GetString(p, 0, len));             
        }
        
        [TearDown]
        public void TearDown()
        {
            foreach (string file in files) {
                File.Delete(file);
            }
        }
    }
}
