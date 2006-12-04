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
using NUnit.Framework;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class BitFieldTest
    {

        private BitField bf;
        private int length;

        [SetUp]
        public void SetUp()
        {
            this.length = 10;
            bf = new BitField(length);
        }

        [Test]
        public void GetterSetter()
        {
            bf[0] = true;
            bf[1] = true;
            bf[3] = true;
            bf[5] = true;

            Assert.IsTrue(bf[0]);
            Assert.IsTrue(bf[1]);
            Assert.IsTrue(!bf[2]);
            Assert.IsTrue(bf[3]);
            Assert.IsTrue(!bf[4]);
            Assert.IsTrue(bf[5]);
            Assert.IsTrue(!bf[6]);
            Assert.IsTrue(!bf[7]);
            Assert.IsTrue(!bf[8]);
            Assert.IsTrue(!bf[9]);
        }

        [Test]
        public void Clone()
        {
            BitField bf2 = (BitField)bf.Clone();

            Assert.AreEqual(bf.AllFalse(), bf2.AllFalse(), "AllFalse wrong");
            Assert.AreEqual(bf.FirstTrue(), bf2.FirstTrue(), "FirstTrue wrong");
            Assert.AreEqual(bf.Length, bf2.Length, "Length wrong");
            Assert.AreEqual(bf.LengthInBytes, bf2.LengthInBytes, "Length in bytes wrong");

            bf[4] = true;
            bf[7] = true;

            Assert.IsTrue(!bf2[4], "Clone changed1");
            Assert.IsTrue(!bf2[7], "Clone changed");
        }

        [Test]
        public void Length()
        {
            Assert.AreEqual(length, bf.Length);
        }

        [Test]
        public void LengthInBytes()
        {
            Assert.AreEqual((int)Math.Ceiling(this.length / 8.0), bf.LengthInBytes);
        }

        [Test]
        public void And()
        {
            BitField bf1 = new BitField(5);
            BitField bf2 = new BitField(5);

            bf1[0] = true; bf2[0] = true;
            bf1[4] = true; bf2[4] = true;

            bf1[3] = true;
            bf2[2] = true;

            bf1.And(bf2);

            Assert.IsTrue(bf1[0]);
            Assert.IsTrue(!bf1[1]);
            Assert.IsTrue(!bf1[2]);
            Assert.IsTrue(!bf1[3]);
            Assert.IsTrue(bf1[4]);

            Assert.IsTrue(bf2[0]);
            Assert.IsTrue(!bf2[1]);
            Assert.IsTrue(bf2[2]);
            Assert.IsTrue(!bf2[3]);
            Assert.IsTrue(bf2[4]);
        }

        [Test]
        public void Or()
        {
            BitField bf1 = new BitField(10);
            BitField bf2 = new BitField(10);
            bf1[0] = true;
            bf1[4] = true;
            bf2[3] = true;
            bf2[5] = true;
            bf2[7] = true;

            bf1.Or(bf2);

            Assert.IsTrue(bf1[0], "a");
            Assert.IsTrue(!bf1[1], "b");
            Assert.IsTrue(!bf1[2], "c");
            Assert.IsTrue(bf1[3], "d");
            Assert.IsTrue(bf1[4], "e");
            Assert.IsTrue(bf1[5], "f");
            Assert.IsTrue(!bf1[6], "g");
            Assert.IsTrue(bf1[7], "h");
            Assert.IsTrue(!bf1[8], "i");
            Assert.IsTrue(!bf1[9], "j");


            Assert.IsTrue(!bf2[0], "k");
            Assert.IsTrue(!bf2[1], "l");
            Assert.IsTrue(!bf2[2], "m");
            Assert.IsTrue(bf2[3], "n");
            Assert.IsTrue(!bf2[4], "o");
            Assert.IsTrue(bf2[5], "p");
            Assert.IsTrue(!bf2[6], "q");
            Assert.IsTrue(bf2[7], "r");
            Assert.IsTrue(!bf2[8], "s");
            Assert.IsTrue(!bf2[9], "t");
        }

        [Test]
        public void Not()
        {
            BitField bf1 = new BitField(5);

            bf1[0] = true;
            bf1[3] = true;
            bf1[4] = true;

            bf1.Not();

            Assert.IsTrue(!bf1[0], "a");
            Assert.IsTrue(bf1[1], "b");
            Assert.IsTrue(bf1[2], "c");
            Assert.IsTrue(!bf1[3], "d");
            Assert.IsTrue(!bf1[4], "e");
        }
    }
}