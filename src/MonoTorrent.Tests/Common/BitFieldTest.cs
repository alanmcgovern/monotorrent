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
using Xunit;
using MonoTorrent.Client;
using System.Collections.Generic;

namespace MonoTorrent.Common
{
    
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

        [Fact]
        public void ConstructorIntTest()
        {
            BitField bf2 = new BitField(initialByteValues, initalValues.Length);
            Assert.Equal(bf, bf2, "#1");
            Assert.Equal(Toolbox.Count<bool>(initalValues, delegate(bool b) { return b; }), bf2.TrueCount, "#1");
        }

        [Fact]
        public void ConstructorBoolTest()
        {
            for (int i = 0; i < initalValues.Length; i++)
                Assert.Equal(initalValues[i], bf[i], "#1:{0}", i);

            Assert.Equal(Toolbox.Count<bool>(initalValues, delegate(bool b) { return b; }), bf.TrueCount, "#1");
        }

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

        [Fact]
        public void FirstTrue()
        {
            Assert.Equal(0, bf.FirstTrue(0, bf.Length));
            Assert.Equal(0, bf.FirstTrue(0, 0));
            Assert.Equal(-1, bf.FirstTrue(bf.Length, bf.Length));
            Assert.Equal(11, bf.FirstTrue(bf.Length - 1, bf.Length - 1));
            Assert.Equal(11, bf.FirstTrue(bf.Length - 1, bf.Length));
            Assert.Equal(11, bf.FirstTrue(9, bf.Length));
        }

        [Fact]
        public void FirstTrue_2()
        {
            BitField b = new BitField(1025);
            b[1024] = true;
            Assert.Equal(1024, b.FirstTrue(0, 1025));
        }

        [Fact]
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
            Assert.Equal(b, bf, "#1");
        }

        [Fact]
        public void ToByteArray()
        {
            BitField first = new BitField(new bool[] { true, false, true, false, true, false, true, true, true, false, false });
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }

        [Fact]
        public void ToByteArray2()
        {
            BitField first = new BitField(new bool[] { true, false, true, false, true, false, true, true, true, false, false, true });
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }

        [Fact]
        public void ToByteArray3()
        {
            BitField first = new BitField(new bool[] { true, false, true, false, true, false, true, true, true, false, false, true, false });
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }

        [Fact]
        public void ToByteArray4()
        {
            BitField first = new BitField(new bool[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true, false});
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }

        [Fact]
        public void ToByteArray5()
        {
            BitField first = new BitField(new bool[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true});
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }

        [Fact]
        public void ToByteArray6()
        {
            BitField first = new BitField(new bool[] {  true, false, true, false, true, false, true, false, true,
                                                        false, false, true, false, true, false, true, false,
                                                        true, false, false, false, true, true, true, false, true,
                                                        true, false, false, true, false, false, true});
            BitField second = new BitField(first.ToByteArray(), first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i], "#" + i);
            }
        }


        [Fact]
        public void Clone()
        {
            BitField clone = bf.Clone();
            Assert.Equal(bf, clone);
        }

        [Fact]
        public void LargeBitfield()
        {
            BitField bf = new BitField(1000);
            bf.SetAll(true);
            Assert.Equal(1000, bf.TrueCount);
        }

        [Fact]
        public void Length()
        {
            Assert.Equal(initalValues.Length, bf.Length);
        }

        [Fact]
        public void LengthInBytes()
        {
            Assert.Equal(1, new BitField(1).LengthInBytes, "#1");
            Assert.Equal(1, new BitField(8).LengthInBytes, "#2");
            Assert.Equal(2, new BitField(9).LengthInBytes, "#3");
            Assert.Equal(2, new BitField(15).LengthInBytes, "#4");
            Assert.Equal(2, new BitField(16).LengthInBytes, "#5");
            Assert.Equal(3, new BitField(17).LengthInBytes, "#6");
        }

        [Fact]
        public void And()
        {
            BitField bf2 = new BitField(secondValues);
            bf.And(bf2);

            Assert.Equal(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.Equal(initalValues[i] && secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] && secondValues[i])
                    count++;

            Assert.Equal(count, bf.TrueCount, "#3");
        }

        [Fact]
        public void And2 ()
        {
            Random r = new Random ();
            byte [] a = new byte [100];
            byte [] b = new byte [100];

            r.NextBytes (a);
            r.NextBytes (b);

            for (int i = 0; i < a.Length * 8; i++) {
                BitField first = new BitField (a, i);
                BitField second = new BitField (b, i);

                first.And (second);
            }
        }

        [Fact]
        public void Or()
        {
            BitField bf2 = new BitField(secondValues);
            bf.Or(bf2);

            Assert.Equal(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.Equal(initalValues[i] || secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] || secondValues[i])
                    count++;

            Assert.Equal(count, bf.TrueCount, "#3");
        }

        [Fact]
        public void Not()
        {
            bf.Not();
            for (int i = 0; i < bf.Length; i++)
                Assert.Equal(!initalValues[i], bf[i], "#1");

            Assert.Equal(Toolbox.Count<bool>(initalValues, delegate(bool b) { return !b; }), bf.TrueCount, "#2");
        }

        [Fact]
        public void Xor()
        {
            BitField bf2 = new BitField(secondValues);
            bf.Xor(bf2);

            Assert.Equal(new BitField(secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.Equal((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]), bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if ((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]))
                    count++;

            Assert.Equal(count, bf.TrueCount, "#3");
        }

        [Fact]
        public void From()
        {
            BitField b = new BitField(31);
            b.SetAll(true);
            Assert.Equal(31, b.TrueCount, "#1");
            Assert.True(b.AllTrue, "#1b");

            b = new BitField(32);
            b.SetAll(true);
            Assert.Equal(32, b.TrueCount, "#2");
            Assert.True(b.AllTrue, "#2b");

            b = new BitField(33);
            b.SetAll(true);
            Assert.Equal(33, b.TrueCount, "#3");
            Assert.True(b.AllTrue, "#3b");
        }
    }
}