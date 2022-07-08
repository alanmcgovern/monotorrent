//
// BitFieldTest.cs
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
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class InfoHashesTests
    {
        public static InfoHash CreateV1 () => new InfoHash (new byte[] {
            2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40
        });

        public static InfoHash CreateV2 () => new InfoHash (new byte[] {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        });

        [Test]
        public void CreateInvalid ()
        {
            Assert.Throws<ArgumentException> (() => new InfoHashes (new InfoHash (new byte[32]), new InfoHash (new byte[32])));
            Assert.Throws<ArgumentException> (() => new InfoHashes (new InfoHash (new byte[20]), new InfoHash (new byte[20])));
            Assert.Throws<ArgumentException> (() => new InfoHashes (new InfoHash (new byte[32]), new InfoHash (new byte[20])));
        }

        [Test]
        public void Create ()
        {
            Assert.DoesNotThrow (() => new InfoHashes (new InfoHash (new byte[20]), new InfoHash (new byte[32])));
        }

        [Test]
        public void Contains ()
        {
            Assert.IsTrue (new InfoHashes (CreateV1 (), CreateV2 ()).Contains (CreateV1 ()));
            Assert.IsTrue (new InfoHashes (CreateV1 (), CreateV2 ()).Contains (CreateV1 ().Truncate ()));

            Assert.IsTrue (new InfoHashes (CreateV1 (), CreateV2 ()).Contains (CreateV2 ().Truncate ()));
            Assert.IsTrue (new InfoHashes (CreateV1 (), CreateV2 ()).Contains (CreateV2 ().Truncate ()));
        }

        [Test]
        public void Expand ()
        {
            var hashes = InfoHashes.FromV2 (new InfoHash (Enumerable.Repeat<byte> (2, 32).ToArray ()));
            Assert.AreEqual (hashes.V2.Span.Length, hashes.Expand (hashes.V2.Truncate ()).Span.Length);
        }
    }
}
