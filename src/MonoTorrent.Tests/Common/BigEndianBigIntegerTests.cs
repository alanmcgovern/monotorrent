using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class BigEndianBigIntegerTests
    {
        [Test]
        public void Zero_Create ()
        {
            var zero = new BigEndianBigInteger(0);

            Assert.AreEqual(new BigEndianBigInteger (new byte[0]), zero);
            Assert.AreEqual(new BigEndianBigInteger (new byte[2]), zero);
            Assert.AreEqual(new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[0])), zero);
            Assert.AreEqual(new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[1])), zero);
            Assert.AreEqual(new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor (new byte[2])), zero);
        }

        [Test]
        public void Zero_ToByteArray()
        {
            var result = new BigEndianBigInteger(0).ToByteArray();
            Assert.IsTrue(result.Length == 0 || result.Single () == 0);
            Assert.IsEmpty(BigEndianBigInteger.FallbackToBigEndianByteArray(new BigEndianBigInteger(0)));

            // Check several arrays
            foreach (var array in new byte[][] { new byte[0], new byte[1], new byte[2] }) {
                var fastPath = new BigEndianBigInteger(array);
                var slowPath = new BigEndianBigInteger (BigEndianBigInteger.FallbackConstructor(array));

                foreach (var value in new[] { fastPath, slowPath })
                {
                    result = value.ToByteArray();
                    Assert.IsTrue(result.Length == 0 || result.Single () == 0);
                    Assert.IsEmpty(BigEndianBigInteger.FallbackToBigEndianByteArray (value));
                }
            }
        }
    }
}
