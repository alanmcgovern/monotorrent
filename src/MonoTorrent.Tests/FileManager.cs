//
// FileManager.cs
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



using System.IO;
using NUnit.Framework;
using System.Text;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    //[TestFixture]
    public class FileManagerTest
    {
        private string path = string.Empty;
        private string directoryName = string.Empty;
        private string fullPath;

        /// <summary>
        /// 
        /// </summary>
        [SetUp]
        public void Setup()
        {
            this.path = GetType().Assembly.Location;
            for (int i = 0; i >= 0; i++)
                if (!Directory.Exists("temp" + i.ToString()))
                {
                    this.directoryName = "temp" + i.ToString();
                    this.fullPath = Path.Combine(this.path, this.directoryName);
                    Directory.CreateDirectory(fullPath);
                    break;
                }

            GenerateTestFiles();
        }

        /// <summary>
        /// 
        /// </summary>
        private void GenerateTestFiles()
        {
            FileStream file1 = File.OpenWrite(Path.Combine(this.fullPath, "file1.txt"));
            FileStream file2 = File.OpenWrite(Path.Combine(this.fullPath, "file2.txt"));

            string data = "this is my teststring. It's not really that long, but i'll be writing a lot more where this come from\r\n";

            for (int i = 0; i < 100; i++)
                file1.Write(System.Text.Encoding.UTF8.GetBytes(data), 0, System.Text.Encoding.UTF8.GetByteCount(data));

            for (int i = 0; i < 5000; i++)
                file2.Write(System.Text.Encoding.UTF8.GetBytes(data), 0, System.Text.Encoding.UTF8.GetByteCount(data));

            file1.Close();
            file2.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        [TearDown]
        public void RemoveTempFiles()
        {
            foreach (string str in Directory.GetFiles(Path.Combine(this.path, this.directoryName)))
                File.Delete(str);

            Directory.Delete(Path.Combine(path, "temp"));
        }
    }
}