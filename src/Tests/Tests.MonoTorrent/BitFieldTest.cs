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
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class BitFieldTest
    {
        MutableBitField bf;
        bool[] initalValues;
        byte[] initialByteValues;
        bool[] secondValues;

        [SetUp]
        public void SetUp ()
        {
            // The bool[] must be kept in sync with the byte[] constructor. They represent exactly the same thing.
            initalValues = new[] { true, false, true, false, true, false, true, true, true, false, false, true };
            secondValues = new[] { true, true, false, false, true, false, true, false, true, false, false, true };
            initialByteValues = new byte[] { 171, 144 };
            bf = new MutableBitField (initalValues);
        }

        [Test]
        public void Constructor_Null ()
        {
            Assert.Throws<ArgumentNullException> (() => new BitField ((BitField) null));
            Assert.Throws<ArgumentNullException> (() => new BitField ((bool[]) null));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (null, 2));

        }
        [Test]
        public void Constructor_TooSmall ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (new bool[0]));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (0));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (-1));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (new byte[0], 1));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (new byte[1], 0));
        }
        [Test]
        public void ConstructorIntTest ()
        {
            BitField bf2 = new BitField (initialByteValues, initalValues.Length);
            Assert.AreEqual (bf, bf2, "#1");
            Assert.AreEqual (initalValues.Count (b => b), bf2.TrueCount, "#1");
        }

        [Test]
        public void ConstructorBoolTest ()
        {
            for (int i = 0; i < initalValues.Length; i++)
                Assert.AreEqual (initalValues[i], bf[i], "#1:{0}", i);

            Assert.AreEqual (initalValues.Count (b => b), bf.TrueCount, "#1");
        }

        [Test]
        public void FirstTrue ()
        {
            Assert.AreEqual (0, bf.FirstTrue (0, bf.Length - 1));
            Assert.AreEqual (0, bf.FirstTrue (0, 0));
            Assert.AreEqual (11, bf.FirstTrue (bf.Length - 2, bf.Length - 1));
            Assert.AreEqual (11, bf.FirstTrue (bf.Length - 1, bf.Length - 1));
            Assert.AreEqual (11, bf.FirstTrue (9, bf.Length - 1));
        }

        [Test]
        public void FirstTrue_2 ()
        {
            var b = new MutableBitField (1025);
            b[1024] = true;
            Assert.AreEqual (1024, b.FirstTrue (0, b.Length - 1));
        }

        [Test]
        public void LongByteArrayTest ()
        {
            List<byte> list = new List<byte> (initialByteValues);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);
            list.Add (byte.MaxValue);

            BitField b = new BitField (list.ToArray (), initalValues.Length);
            Assert.AreEqual (b, bf, "#1");
        }

        [Test]
        public void ToByteArray ()
        {
            BitField first = new BitField (new[] { true, false, true, false, true, false, true, true, true, false, false });
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray2 ()
        {
            BitField first = new BitField (new[] { true, false, true, false, true, false, true, true, true, false, false, true });
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray3 ()
        {
            BitField first = new BitField (new[] { true, false, true, false, true, false, true, true, true, false, false, true, false });
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray4 ()
        {
            BitField first = new BitField (new[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true, false});
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray5 ()
        {
            BitField first = new BitField (new[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true});
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray6 ()
        {
            BitField first = new BitField (new[] {  true, false, true, false, true, false, true, false, true,
                                                        false, false, true, false, true, false, true, false,
                                                        true, false, false, false, true, true, true, false, true,
                                                        true, false, false, true, false, false, true});
            BitField second = new BitField (first.ToByteArray (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void Clone ()
        {
            BitField clone = new BitField (bf);
            Assert.AreEqual (bf, clone);
            Assert.IsTrue (bf.Equals (clone));
            Assert.AreEqual (bf.GetHashCode (), clone.GetHashCode ());
        }

        [Test]
        public void Get_OutOfRange ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => GC.KeepAlive (new BitField (10)[-1]));
            Assert.Throws<ArgumentOutOfRangeException> (() => GC.KeepAlive (new BitField (10)[10]));
        }

        [Test]
        public void LargeBitfield ()
        {
            var bf = new MutableBitField (1000);
            bf.SetAll (true);
            Assert.AreEqual (1000, bf.TrueCount);
        }

        [Test]
        public void Length ()
        {
            Assert.AreEqual (initalValues.Length, bf.Length);
        }

        [Test]
        public void LengthInBytes ()
        {
            Assert.AreEqual (1, new BitField (1).LengthInBytes, "#1");
            Assert.AreEqual (1, new BitField (8).LengthInBytes, "#2");
            Assert.AreEqual (2, new BitField (9).LengthInBytes, "#3");
            Assert.AreEqual (2, new BitField (15).LengthInBytes, "#4");
            Assert.AreEqual (2, new BitField (16).LengthInBytes, "#5");
            Assert.AreEqual (3, new BitField (17).LengthInBytes, "#6");
        }

        [Test]
        public void And ()
        {
            BitField bf2 = new BitField (secondValues);
            bf.And (bf2);

            Assert.AreEqual (new BitField (secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual (initalValues[i] && secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] && secondValues[i])
                    count++;

            Assert.AreEqual (count, bf.TrueCount, "#3");
        }

        [Test]
        public void And2 ()
        {
            Random r = new Random ();
            byte[] a = new byte[100];
            byte[] b = new byte[100];

            r.NextBytes (a);
            r.NextBytes (b);

            for (int i = 1; i < a.Length * 8; i++) {
                var first = new MutableBitField (a, i);
                var second = new MutableBitField (b, i);

                first.And (second);
            }
        }

        [Test]
        public void And_DifferentLength ()
        {
            Assert.Throws<ArgumentException> (() => new MutableBitField (10).And (new MutableBitField (3)));
        }

        [Test]
        public void CountTrue_InvalidSelector ()
        {
            Assert.Throws<ArgumentNullException> (() => new BitField (10).CountTrue (null));
            Assert.Throws<ArgumentException> (() => new BitField (10).CountTrue (new BitField (5)));
        }

        [Test]
        public void Equals_False ()
        {
            var bf = new MutableBitField (10).SetAll (true);
            var other = new MutableBitField (bf).Set (5, false);
            Assert.IsFalse (bf.Equals (other));
            Assert.IsFalse (bf.Equals (null));
            Assert.IsFalse (bf.Equals (new BitField (5)));

            bf.Set (6, false);
            Assert.AreEqual (bf.TrueCount, other.TrueCount);
            Assert.IsFalse (bf.Equals (other));
        }

        [Test]
        public void Or ()
        {
            BitField bf2 = new BitField (secondValues);
            bf.Or (bf2);

            Assert.AreEqual (new BitField (secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual (initalValues[i] || secondValues[i], bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if (initalValues[i] || secondValues[i])
                    count++;

            Assert.AreEqual (count, bf.TrueCount, "#3");
        }

        [Test]
        public void Set_OutOfRange ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new MutableBitField (10).Set (-1, true));
            Assert.Throws<ArgumentOutOfRangeException> (() => new MutableBitField (10).Set (10, true));
        }

        [Test]
        public void Not ()
        {
            bf.Not ();
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual (!initalValues[i], bf[i], "#1");

            Assert.AreEqual (initalValues.Count (b => !b), bf.TrueCount, "#2");
        }

        [Test]
        public void Xor ()
        {
            MutableBitField bf2 = new MutableBitField (secondValues);
            bf.Xor (bf2);

            Assert.AreEqual (new BitField (secondValues), bf2, "#1: bf2 should be unmodified");
            for (int i = 0; i < bf.Length; i++)
                Assert.AreEqual ((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]), bf[i], "#2");

            int count = 0;
            for (int i = 0; i < initalValues.Length; i++)
                if ((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]))
                    count++;

            Assert.AreEqual (count, bf.TrueCount, "#3");
        }

        [Test]
        public void From ()
        {
            MutableBitField b = new MutableBitField (31);
            b.SetAll (true);
            Assert.AreEqual (31, b.TrueCount, "#1");
            Assert.IsTrue (b.AllTrue, "#1b");

            b = new MutableBitField (32);
            b.SetAll (true);
            Assert.AreEqual (32, b.TrueCount, "#2");
            Assert.IsTrue (b.AllTrue, "#2b");

            b = new MutableBitField (33);
            b.SetAll (true);
            Assert.AreEqual (33, b.TrueCount, "#3");
            Assert.IsTrue (b.AllTrue, "#3b");
        }
    }
}
