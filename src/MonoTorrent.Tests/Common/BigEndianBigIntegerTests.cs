﻿//
// BigEndianBigIntegerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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


using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class BigEndianBigIntegerTests
    {
        [Test]
        public void Zero_Create ()
        {
            var zero = new BigEndianBigInteger (0);

            Assert.AreEqual (new BigEndianBigInteger (new byte[0]), zero);
            Assert.AreEqual (new BigEndianBigInteger (new byte[2]), zero);
            Assert.AreEqual (new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[0])), zero);
            Assert.AreEqual (new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[1])), zero);
            Assert.AreEqual (new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[2])), zero);
        }

        [Test]
        public void Zero_ToByteArray ()
        {
            var result = new BigEndianBigInteger (0).ToByteArray ();
            Assert.IsTrue (result.Length == 0 || result.Single () == 0);
            Assert.IsEmpty (BigEndianBigInteger.FallbackToBigEndianByteArray (new BigEndianBigInteger (0)));

            // Check several arrays
            foreach (var array in new[] { new byte[0], new byte[1], new byte[2] }) {
                var fastPath = new BigEndianBigInteger (array);
                var slowPath = new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (array));

                foreach (var value in new[] { fastPath, slowPath }) {
                    result = value.ToByteArray ();
                    Assert.IsTrue (result.Length == 0 || result.Single () == 0);
                    Assert.IsEmpty (BigEndianBigInteger.FallbackToBigEndianByteArray (value));
                }
            }
        }
    }
}
