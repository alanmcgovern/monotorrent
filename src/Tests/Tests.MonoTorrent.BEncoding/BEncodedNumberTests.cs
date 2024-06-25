//
// BEncodedNumberTests.cs
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
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

namespace MonoTorrent.BEncoding
{

    /// <summary>
    /// 
    /// </summary>
    [TestFixture]
    public class BEncodedNumberTests
    {
        [Test]
        public void benNumberDecoding ()
        {
            foreach (var result in BEncodedValue.DecodingVariants<BEncodedNumber>(Encoding.UTF8.GetBytes ("i12412e"))) {
                Assert.AreEqual (result.ToString (), "12412");
                Assert.AreEqual (result.Number, 12412);
            }
        }

        [Test]
        public void benNumberEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i12345e");
            BEncodedNumber number = 12345;
            Assert.IsTrue (data.AsSpan ().SequenceEqual (number.Encode ()));
        }

        [Test]
        public void benNumberEncoding2 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i0e");
            BEncodedNumber number = 0;
            Assert.AreEqual (3, number.LengthInBytes ());
            Assert.IsTrue (data.AsSpan ().SequenceEqual (number.Encode ()));
        }

        [Test]
        public void benNumberEncoding3 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i1230e");
            BEncodedNumber number = 1230;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (data.AsSpan ().SequenceEqual (number.Encode ()));
        }

        [Test]
        public void benNumberEncoding4 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i-1230e");
            BEncodedNumber number = -1230;
            Assert.AreEqual (7, number.LengthInBytes ());
            Assert.IsTrue (data.AsSpan ().SequenceEqual (number.Encode ()));
        }

        [Test]
        public void benNumberEncoding5 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i-123e");
            BEncodedNumber number = -123;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (data.AsSpan ().SequenceEqual (number.Encode ()));
        }

        [Test]
        public void benNumberEncoding6 ()
        {
            BEncodedNumber a = -123;
            foreach (var b in BEncodedValue.DecodingVariants<BEncodedNumber> (a.Encode ())) {
                Assert.AreEqual (a.Number, b.Number, "#1");
            }
        }

        [Test]
        public void benNumber_MaxMin ([Values (long.MinValue, int.MinValue, 3000000000L, int.MaxValue, uint.MaxValue, long.MaxValue)] long value)
        {
            var number = new BEncodedNumber (value);
            foreach (var result in BEncodedValue.DecodingVariants<BEncodedNumber> (number.Encode ()))
                Assert.AreEqual (result.Number, value);
        }

        [Test]
        public void benNumber_AllPowersOf10 ()
        {
            for (long value = 1L; value > 0; value *= 10) {
                foreach (var offset in new int[] { -1, 0, 1 }) {
                    // positive value
                    var expected = value + offset;
                    var number = new BEncodedNumber (expected);
                    foreach (var result in BEncodedValue.DecodingVariants<BEncodedNumber> (number.Encode ()))
                        Assert.AreEqual (expected, result.Number);

                    // negative value.
                    expected *= -1;
                    number = new BEncodedNumber (expected);
                    foreach (var result in BEncodedValue.DecodingVariants<BEncodedNumber> (number.Encode ()))
                        Assert.AreEqual (expected, result.Number);
                }
            }
        }

        [Test]
        public void benNumberEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i12345e");
            BEncodedNumber number = 12345;
            byte[] result = new byte[number.LengthInBytes ()];
            number.Encode (result);
            Assert.IsTrue (data.AsSpan ().SequenceEqual (result));
        }

        [Test]
        public void benNumberLengthInBytes ()
        {
            int number = 1635;
            BEncodedNumber num = number;
            Assert.AreEqual (number.ToString ().Length + 2, num.LengthInBytes ());
        }

        [Test]
        public void corruptBenNumberDecode ()
        {
            var data = Encoding.UTF8.GetBytes ("i35212");
            Assert.Throws<BEncodingException> (() => {
                BEncodedValue.Decode (data);
            });

            Assert.Throws<BEncodingException> (() => {
                BEncodedValue.Decode (new MemoryStream (data));
            });
        }
    }
}
