using System;
using System.Collections.Generic;
using MonoTorrent.Client;
using Xunit;

namespace MonoTorrent.Common
{
    public class BitFieldTest
    {
        public BitFieldTest()
        {
            // The bool[] must be kept in sync with the byte[] constructor. They represent exactly the same thing.
            initalValues = new[] {true, false, true, false, true, false, true, true, true, false, false, true};
            secondValues = new[] {true, true, false, false, true, false, true, false, true, false, false, true};
            initialByteValues = new byte[] {171, 144};
            bf = new BitField(initalValues);
        }

        private BitField bf;
        private readonly bool[] initalValues;
        private readonly byte[] initialByteValues;
        private readonly bool[] secondValues;

        // "This is deliberately broken to work around bugs in azureus"
        public void InvalidBitfieldTest()
        {
            // Set each of the 4 trailing bits to 1 to force a decode error
            for (byte i = 8; i > 0; i /= 2)
            {
                try
                {
                    initialByteValues[1] += i;
                    bf = new BitField(initialByteValues, initalValues.Length);
                    Assert.True(false, string.Format("The bitfield was corrupt but decoded correctly: Loop {0}", i));
                }
                catch (MessageException)
                {
                    initialByteValues[1] -= i;
                }
            }
        }

        [Fact]
        public void And()
        {
            var bf2 = new BitField(secondValues);
            bf.And(bf2);

            Assert.Equal(new BitField(secondValues), bf2);
            for (var i = 0; i < bf.Length; i++)
                Assert.Equal(initalValues[i] && secondValues[i], bf[i]);

            var count = 0;
            for (var i = 0; i < initalValues.Length; i++)
                if (initalValues[i] && secondValues[i])
                    count++;

            Assert.Equal(count, bf.TrueCount);
        }

        [Fact]
        public void And2()
        {
            var r = new Random();
            var a = new byte[100];
            var b = new byte[100];

            r.NextBytes(a);
            r.NextBytes(b);

            for (var i = 0; i < a.Length*8; i++)
            {
                var first = new BitField(a, i);
                var second = new BitField(b, i);

                first.And(second);
            }
        }


        [Fact]
        public void Clone()
        {
            var clone = bf.Clone();
            Assert.Equal(bf, clone);
        }

        [Fact]
        public void ConstructorBoolTest()
        {
            for (var i = 0; i < initalValues.Length; i++)
                Assert.Equal(initalValues[i], bf[i]);

            Assert.Equal(Toolbox.Count(initalValues, delegate(bool b) { return b; }), bf.TrueCount);
        }

        [Fact]
        public void ConstructorIntTest()
        {
            var bf2 = new BitField(initialByteValues, initalValues.Length);
            Assert.Equal(bf, bf2);
            Assert.Equal(Toolbox.Count(initalValues, delegate(bool b) { return b; }), bf2.TrueCount);
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
            var b = new BitField(1025);
            b[1024] = true;
            Assert.Equal(1024, b.FirstTrue(0, 1025));
        }

        [Fact]
        public void From()
        {
            var b = new BitField(31);
            b.SetAll(true);
            Assert.Equal(31, b.TrueCount);
            Assert.True(b.AllTrue);

            b = new BitField(32);
            b.SetAll(true);
            Assert.Equal(32, b.TrueCount);
            Assert.True(b.AllTrue);

            b = new BitField(33);
            b.SetAll(true);
            Assert.Equal(33, b.TrueCount);
            Assert.True(b.AllTrue);
        }

        [Fact]
        public void LargeBitfield()
        {
            var bf = new BitField(1000);
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
            Assert.Equal(1, new BitField(1).LengthInBytes);
            Assert.Equal(1, new BitField(8).LengthInBytes);
            Assert.Equal(2, new BitField(9).LengthInBytes);
            Assert.Equal(2, new BitField(15).LengthInBytes);
            Assert.Equal(2, new BitField(16).LengthInBytes);
            Assert.Equal(3, new BitField(17).LengthInBytes);
        }

        [Fact]
        public void LongByteArrayTest()
        {
            var list = new List<byte>(initialByteValues);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);
            list.Add(byte.MaxValue);

            var b = new BitField(list.ToArray(), initalValues.Length);
            Assert.Equal(b, bf);
        }

        [Fact]
        public void Not()
        {
            bf.Not();
            for (var i = 0; i < bf.Length; i++)
                Assert.Equal(!initalValues[i], bf[i]);

            Assert.Equal(Toolbox.Count(initalValues, delegate(bool b) { return !b; }), bf.TrueCount);
        }

        [Fact]
        public void Or()
        {
            var bf2 = new BitField(secondValues);
            bf.Or(bf2);

            Assert.Equal(new BitField(secondValues), bf2);
            for (var i = 0; i < bf.Length; i++)
                Assert.Equal(initalValues[i] || secondValues[i], bf[i]);

            var count = 0;
            for (var i = 0; i < initalValues.Length; i++)
                if (initalValues[i] || secondValues[i])
                    count++;

            Assert.Equal(count, bf.TrueCount);
        }

        [Fact]
        public void ToByteArray()
        {
            var first =
                new BitField(new[] {true, false, true, false, true, false, true, true, true, false, false});
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void ToByteArray2()
        {
            var first =
                new BitField(new[] {true, false, true, false, true, false, true, true, true, false, false, true});
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void ToByteArray3()
        {
            var first =
                new BitField(new[]
                {true, false, true, false, true, false, true, true, true, false, false, true, false});
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void ToByteArray4()
        {
            var first = new BitField(new[]
            {
                true, false, true, false, true, false, true, false,
                false, false, true, false, true, false, false, false,
                true, false, false, false, true, true, true, false,
                true, false, false, true, false, false, true, false
            });
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void ToByteArray5()
        {
            var first = new BitField(new[]
            {
                true, false, true, false, true, false, true, false,
                false, false, true, false, true, false, false, false,
                true, false, false, false, true, true, true, false,
                true, false, false, true, false, false, true
            });
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void ToByteArray6()
        {
            var first = new BitField(new[]
            {
                true, false, true, false, true, false, true, false, true,
                false, false, true, false, true, false, true, false,
                true, false, false, false, true, true, true, false, true,
                true, false, false, true, false, false, true
            });
            var second = new BitField(first.ToByteArray(), first.Length);
            for (var i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Fact]
        public void Xor()
        {
            var bf2 = new BitField(secondValues);
            bf.Xor(bf2);

            Assert.Equal(new BitField(secondValues), bf2);
            for (var i = 0; i < bf.Length; i++)
                Assert.Equal((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]), bf[i]);

            var count = 0;
            for (var i = 0; i < initalValues.Length; i++)
                if ((initalValues[i] || secondValues[i]) && !(initalValues[i] && secondValues[i]))
                    count++;

            Assert.Equal(count, bf.TrueCount);
        }
    }
}