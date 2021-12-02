//
// BEncodedListTests.cs
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
using System.Text;

using NUnit.Framework;

namespace MonoTorrent.BEncoding
{

    /// <summary>
    /// 
    /// </summary>
    [TestFixture]
    class BEncodedListTests
    {
        [Test]
        public void benListDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            foreach (var list in BEncodedValue.DecodingVariants<BEncodedList> (data)) {
                Assert.AreEqual (list.Count, 3);
                Assert.AreEqual (list[0] is BEncodedString, true);
                Assert.AreEqual (((BEncodedString) list[0]).Text, "test");
                Assert.AreEqual (((BEncodedString) list[1]).Text, "tests");
                Assert.AreEqual (((BEncodedString) list[2]).Text, "tested");
            }
        }

        [Test]
        public void benListEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList {
                new BEncodedString ("test"),
                new BEncodedString ("tests"),
                new BEncodedString ("tested")
            };

            Assert.IsTrue (data.AsSpan ().SequenceEqual (list.Encode ()));
        }

        [Test]
        public void benListEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList {
                new BEncodedString ("test"),
                new BEncodedString ("tests"),
                new BEncodedString ("tested")
            };
            byte[] result = new byte[list.LengthInBytes ()];
            list.Encode (result);
            Assert.IsTrue (data.AsSpan ().SequenceEqual (result));
        }

        [Test]
        public void benListStackedTest ()
        {
            string benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";
            foreach (var list in BEncodedValue.DecodingVariants<BEncodedList> (Encoding.UTF8.GetBytes (benString))) {
                string decoded = Encoding.UTF8.GetString (list.Encode ());
                Assert.AreEqual (benString, decoded);
            }
        }

        [Test]
        public void benListLengthInBytes ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            foreach (var list in BEncodedValue.DecodingVariants<BEncodedList> (data))
                Assert.AreEqual (data.Length, list.LengthInBytes ());
        }

        [Test]
        public void Decode_DoubleNegative ()
        {
            var data = Encoding.UTF8.GetBytes ("i--1e");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }

        [Test]
        public void Decode_NegativeNoNumber ()
        {
            var data = Encoding.UTF8.GetBytes ("i-e");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }

        [Test]
        public void corruptBenListDecode ()
        {
            var data = Encoding.UTF8.GetBytes ("l3:3521:a3:ae");
            Assert.Throws<BEncodingException> (() =>  BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() =>  BEncodedValue.Decode (new MemoryStream (data)));
        }
    }
}
