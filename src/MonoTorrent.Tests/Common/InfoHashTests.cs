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

namespace MonoTorrent.Common
{
    [TestFixture]
    public class InfoHashTests
    {
        InfoHash Create ()
        {
            return new InfoHash (new byte[] {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Test]
        public void HexTest ()
        {
            InfoHash hash = Create ();
            string hex = hash.ToHex ();
            Assert.AreEqual (40, hex.Length, "#1");
            InfoHash other = InfoHash.FromHex (hex);
            Assert.AreEqual (hash, other, "#2");
        }

        [Test]
        public void InvalidArray ()
        {
            Assert.Throws<ArgumentException> (() => new InfoHash (new byte[21]));
        }

        [Test]
        public void InvalidBase32 ()
        {
            Assert.Throws<ArgumentException> (() => InfoHash.FromBase32 ("YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5"));
            Assert.Throws<ArgumentException> (() => InfoHash.FromBase32 ("YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5?"));
        }

        [Test]
        public void InvalidHex ()
        {
            Assert.Throws<ArgumentException> (() => {
                InfoHash.FromHex ("123123123123123123123");
            });
        }

        [Test]
        public void NullHex ()
        {
            Assert.Throws<ArgumentNullException> (() => {
                InfoHash.FromHex (null);
            });
        }

        [Test]
        public void Equals_False ()
        {
            var first = new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ());
            var second = new InfoHash (Enumerable.Repeat ((byte) 1, 20).ToArray ());
            Assert.IsFalse (first.Equals ((object)second));
            Assert.IsFalse (first.Equals (second));
        }

        [Test]
        public void Equals_True ()
        {
            var first = new InfoHash (Enumerable.Repeat((byte)0, 20).ToArray());
            var second = new InfoHash (Enumerable.Repeat((byte)0, 20).ToArray());
            Assert.IsTrue(first.Equals ((object)second));
            Assert.IsTrue(first.Equals (second));
        }

        [Test]
        public void ParseFromMagnetLin ()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var infoHash = InfoHash.FromMagnetLink ("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C&xl=12345");
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.IsNotNull (infoHash);
        }
    }
}
