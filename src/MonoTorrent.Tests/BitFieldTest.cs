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
using MonoTorrent.Client;
using System.Collections.Generic;

namespace MonoTorrent.Common.Tests
{
    [TestFixture]
    public class BitFieldTest
    {
        BitField bf;
        bool[] initalValues;
        byte[] initialByteValues;
        bool[] secondValues;

        [SetUp]
        public void SetUp()
        {
            // The bool[] must be kept in sync with the byte[] constructor. They represent exactly the same thing.
            initalValues = new bool[] { true, false, true, false, true, false, true, true, true, false, false, true };
            secondValues = new bool[] { true, true, false, false, true, false, true, false, true, false, false, true };
            initialByteValues = new byte[] { 171, 144 };
            bf = new BitField(initalValues);
        }

        [Test]
        public void ConstructorIntTest()
        {
            BitField bf2 = new BitField(initialByteValues, initalValues.Length);
            Assert.AreEqual(bf, bf2, "#1");
            Assert.AreEqual(Toolbox.Count<bool>(initalValues, delegate(bool b) { return b; }), bf2.TrueCount, "#1");
        }

        [Test]
        public void ConstructorBoolTest()
        {
            for (int i = 0; i < initalValues.Length; i++)
                Assert.AreEqual(initalValues[i], bf[i], "#1:{0}", i);

            Assert.AreEqual(Toolbox.Count<bool>(initalValues, delegate(bool b) { return b; }), bf.TrueCount, "#1");
        }

        [Test]
        [Ignore("This is deliberately broken to work around bugs in azureus")]
        public void InvalidBitfieldTest()
        {
            // Set each of the 4 trailing bits to 1 to force a decode error
            for (byte i = 8; i > 0; i /= 2)
            {
                try
                {
                    initialByteValues[1] += i;
                    bf = new BitField(initialByteValues, initalValues.Length);
                    Assert.Fail("The bitfield was corrupt but decoded correctly: Loop {0}", i);
                }
                catch (MessageException) { initialByteValues[1] -= i; }
            }
        }

        [Test]
        public void FirstTrue()
        {
            Assert.AreEqual(0, bf.FirstTrue(0, bf.Length));
            Assert.AreEqual(0, bf.FirstTrue(0, 0));
            Assert.AreEqual(-1, bf.FirstTrue(bf.Length, bf.Length));
            Assert.AreEqual(11, bf.FirstTrue(bf.Length - 1, bf.Length - 1));
            Assert.AreEqual(11, bf.FirstTrue(bf.Length - 1, bf.Length));
            Assert.AreEqual(11, bf.FirstTrue(9, bf.Length));
        }

        [Test]
        public void LongByteArrayTest()
        {
            List<byte> list = new List<byte>(initialByteValues);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);

            BitField b = new BitField(list.ToArray(), initalValues.Length);
            Assert.AreEqual(b, bf, "#1");
        }

        [Test]
        public void Clone()
        {
            BitField clone = bf.Clone();
            Assert.AreEqual(bf, clone);
        }

        [Test]
        public void Length()
        {
            Assert.AreEqual(initalValues.Length, bf.Length);
        }

        [Test]
        public void LengthInBytes()
        {
            Assert.AreEqual((int)Math.Ceiling(initalValues.Length / 8.0), bf.LengthInBytes);
        }

        [Test]
        public void And()
        {
            BitField bf2 = new BitField(secondValues);
            bf.And(bf2);

            Assert.AreEqual(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual(initalValues[i] && secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] && secondValues[i])
                    count++;

            Assert.AreEqual(count, bf.TrueCount, "#3");
        }

        [Test]
        public void Or()
        {
            BitField bf2 = new BitField(secondValues);
            bf.Or(bf2);

            Assert.AreEqual(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual(initalValues[i] || secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] || secondValues[i])
                    count++;

            Assert.AreEqual(count, bf.TrueCount, "#3");
        }

        [Test]
        public void Not()
        {
            bf.Not();
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual(!initalValues[i], bf[i], "#1");

            Assert.AreEqual(Toolbox.Count<bool>(initalValues, delegate(bool b) { return !b; }), bf.TrueCount, "#2");
        }

        [Test]
        public void Xor()
        {
            BitField bf2 = new BitField(secondValues);
            bf.Xor(bf2);

            Assert.AreEqual(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]), bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if ((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]))
                    count++;

            Assert.AreEqual(count, bf.TrueCount, "#3");
        }
    }
}