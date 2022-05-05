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
        BitField bf;
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
            bf = new BitField (initalValues);
        }

        [Test]
        public void Constructor_Null ()
        {
            Assert.Throws<ArgumentNullException> (() => new ReadOnlyBitField ((ReadOnlyBitField) null));
            Assert.Throws<ArgumentNullException> (() => new ReadOnlyBitField ((bool[]) null));
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (null, 2));

        }
        [Test]
        public void Constructor_TooSmall ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (Array.Empty<bool> ()));
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (0));
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (-1));
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (Array.Empty<byte> (), 1));
            Assert.Throws<ArgumentOutOfRangeException> (() => new ReadOnlyBitField (new byte[1], 0));
        }
        [Test]
        public void ConstructorIntTest ()
        {
            ReadOnlyBitField bf2 = new ReadOnlyBitField (initialByteValues, initalValues.Length);
            Assert.IsTrue (bf.SequenceEqual (bf2), "#1");
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
        public void ExtraBitsAtEnd ()
        {
            ReadOnlyBitField bf = new BitField (17).From (new byte[] { byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue });
            Assert.AreEqual (17, bf.TrueCount);
            Assert.IsTrue (new[] { byte.MaxValue, byte.MaxValue, (byte)(1 << 7) }.SequenceEqual (bf.ToBytes ()));
        }

        [Test]
        public void ExtraBitsAtEnd2 ()
        {
            ReadOnlyBitField bf = new BitField (32).From (new byte[] { byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue });
            Assert.AreEqual (32, bf.TrueCount);
            Assert.IsTrue (new[] { byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue }.SequenceEqual (bf.ToBytes ()));
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
            var b = new BitField (1025);
            b[1024] = true;
            Assert.AreEqual (1024, b.FirstTrue (0, b.Length - 1));
        }

        [Test]
        public void FirstTrue_3 ()
        {
            var b = new BitField (65);
            b[0] = true;
            Assert.AreEqual (0, b.FirstTrue (0, 0));
            Assert.AreEqual (0, b.FirstTrue (0, 1));
            Assert.AreEqual (-1, b.FirstTrue (1, 1));
            Assert.AreEqual (-1, b.FirstTrue (1, 2));

            b[31] = true;
            Assert.AreEqual (31, b.FirstTrue (1, 31));
            Assert.AreEqual (31, b.FirstTrue (1, 32));

            b[31] = false;
            b[32] = true;
            Assert.AreEqual (32, b.FirstTrue (1, 32));
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

            ReadOnlyBitField b = new ReadOnlyBitField (list.ToArray (), initalValues.Length);
            Assert.IsTrue (b.SequenceEqual (bf), "#1");
        }

        [Test]
        public void ToByteArray ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] { true, false, true, false, true, false, true, true, true, false, false });
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray2 ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] { true, false, true, false, true, false, true, true, true, false, false, true });
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray3 ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] { true, false, true, false, true, false, true, true, true, false, false, true, false });
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray4 ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true, false});
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray5 ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] {  true, false, true, false, true, false, true, false,
                                                        false, false, true, false, true, false, false, false,
                                                        true, false, false, false, true, true, true, false,
                                                        true, false, false, true, false, false, true});
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void ToByteArray6 ()
        {
            ReadOnlyBitField first = new ReadOnlyBitField (new[] {  true, false, true, false, true, false, true, false, true,
                                                        false, false, true, false, true, false, true, false,
                                                        true, false, false, false, true, true, true, false, true,
                                                        true, false, false, true, false, false, true});
            ReadOnlyBitField second = new ReadOnlyBitField (first.ToBytes (), first.Length);
            for (int i = 0; i < first.Length; i++) {
                Assert.AreEqual (first[i], second[i], "#" + i);
            }
        }

        [Test]
        public void Clone ()
        {
            ReadOnlyBitField clone = new ReadOnlyBitField (bf);
            Assert.IsTrue (bf.SequenceEqual (clone));
            Assert.IsTrue (bf.SequenceEqual (clone));
        }

        [Test]
        public void Get_OutOfRange ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => GC.KeepAlive (new ReadOnlyBitField (10)[-1]));
            Assert.Throws<ArgumentOutOfRangeException> (() => GC.KeepAlive (new ReadOnlyBitField (10)[10]));
        }

        [Test]
        public void LargeBitfield ()
        {
            var bf = new BitField (1000);
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
            Assert.AreEqual (1, new ReadOnlyBitField (1).LengthInBytes, "#1");
            Assert.AreEqual (1, new ReadOnlyBitField (8).LengthInBytes, "#2");
            Assert.AreEqual (2, new ReadOnlyBitField (9).LengthInBytes, "#3");
            Assert.AreEqual (2, new ReadOnlyBitField (15).LengthInBytes, "#4");
            Assert.AreEqual (2, new ReadOnlyBitField (16).LengthInBytes, "#5");
            Assert.AreEqual (3, new ReadOnlyBitField (17).LengthInBytes, "#6");
        }

        [Test]
        public void And ()
        {
            ReadOnlyBitField bf2 = new ReadOnlyBitField (secondValues);
            bf.And (bf2);

            Assert.IsTrue (new ReadOnlyBitField (secondValues).SequenceEqual (bf2), "#1: bf2 should be unmodified");
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
                var first = new BitField (a, i);
                var second = new BitField (b, i);

                first.And (second);
            }
        }

        [Test]
        public void And_DifferentLength ()
        {
            Assert.Throws<ArgumentException> (() => new BitField (10).And (new BitField (3)));
        }

        [Test]
        public void CountTrue_InvalidSelector ()
        {
            Assert.Throws<ArgumentNullException> (() => new ReadOnlyBitField (10).CountTrue (null));
            Assert.Throws<ArgumentException> (() => new ReadOnlyBitField (10).CountTrue (new ReadOnlyBitField (5)));
        }

        [Test]
        public void Equals_False ()
        {
            var bf = new BitField (10).SetAll (true);
            var other = new BitField (bf).Set (5, false);
            Assert.IsFalse (bf.Equals (other));
            Assert.IsFalse (bf.Equals (null));
            Assert.IsFalse (bf.Equals (new ReadOnlyBitField (5)));

            bf.Set (6, false);
            Assert.AreEqual (bf.TrueCount, other.TrueCount);
            Assert.IsFalse (bf.Equals (other));
        }

        [Test]
        public void Or ()
        {
            ReadOnlyBitField bf2 = new ReadOnlyBitField (secondValues);
            bf.Or (bf2);

            Assert.IsTrue (new ReadOnlyBitField (secondValues).SequenceEqual (bf2), "#1: bf2 should be unmodified");
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
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (10).Set (-1, true));
            Assert.Throws<ArgumentOutOfRangeException> (() => new BitField (10).Set (10, true));
        }

        [Test]
        public void NAnd ()
        {
            ReadOnlyBitField allTrue = new BitField (10).SetAll (true);
            ReadOnlyBitField allFalse = new BitField (10).SetAll (false);

            Assert.IsTrue (new BitField (allTrue).NAnd (allTrue).AllFalse);
            Assert.IsTrue (new BitField (allTrue).NAnd (allFalse).AllTrue);
            Assert.IsTrue (new BitField (allFalse).NAnd (allTrue).AllFalse);
            Assert.IsTrue (new BitField (allFalse).NAnd (allFalse).AllFalse);
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
        public void Not_ExtraBits ()
        {
            var bf = new BitField (25);
            Assert.AreEqual (0, bf.TrueCount);
            Assert.IsTrue (new byte[] { 0, 0, 0, 0 }.SequenceEqual (bf.ToBytes ()));

            bf.Not ();
            Assert.AreEqual (25, bf.TrueCount);
            Assert.IsTrue (new byte[] { byte.MaxValue, byte.MaxValue, byte.MaxValue, 1 << 7 }.SequenceEqual (bf.ToBytes ()));

            bf.Not ();
            Assert.AreEqual (0, bf.TrueCount);
            Assert.IsTrue (new byte[] { 0, 0, 0, 0 }.SequenceEqual (bf.ToBytes ()));
        }

        [Test]
        public void Xor ()
        {
            BitField bf2 = new BitField (secondValues);
            bf.Xor (bf2);

            Assert.IsTrue (new ReadOnlyBitField (secondValues).SequenceEqual (bf2), "#1: bf2 should be unmodified");
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
            BitField b = new BitField (31);
            b.SetAll (true);
            Assert.AreEqual (31, b.TrueCount, "#1");
            Assert.IsTrue (b.AllTrue, "#1b");

            b = new BitField (32);
            b.SetAll (true);
            Assert.AreEqual (32, b.TrueCount, "#2");
            Assert.IsTrue (b.AllTrue, "#2b");

            b = new BitField (33);
            b.SetAll (true);
            Assert.AreEqual (33, b.TrueCount, "#3");
            Assert.IsTrue (b.AllTrue, "#3b");
        }
    }
}
