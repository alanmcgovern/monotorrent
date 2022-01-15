//
// PathValidatorTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class PathValidatorTests
    {
        [Test]
        public void UnixRelative ()
        {
            Assert.Throws<ArgumentException> (() => PathValidator.Validate ("../foo/bar.txt"));
            Assert.Throws<ArgumentException> (() => PathValidator.Validate ("foo/../bar"));
        }

        [Test]
        public void UnixRooted ()
        {
            Assert.Throws<ArgumentException> (() => PathValidator.Validate ("/"));
            Assert.Throws<ArgumentException> (() => PathValidator.Validate ("/foo"));
            Assert.Throws<ArgumentException> (() => PathValidator.Validate ("/.."));
        }

        [Test]
        public void WindowsRelative ()
        {
            Assert.Throws<ArgumentException> (() => PathValidator.Validate (@"..\foo\bar.txt"));
            Assert.Throws<ArgumentException> (() => PathValidator.Validate (@"test\..\bar.txt"));
        }

        [Test]
        public void WindowsRooted ()
        {
            Assert.Throws<ArgumentException> (() => PathValidator.Validate (@"C:\test.txt"));
        }
    }
}
