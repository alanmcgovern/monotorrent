using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class BitOpsTests
    {
        [Test]
        public void CeilLog2_int ()
        {
            for (int i = 0; i < 31; i++)
                Assert.AreEqual (i, BitOps.CeilLog2 (1 << i));

            Assert.AreEqual (31, BitOps.CeilLog2 (Int32.MaxValue));
        }

        [Test]
        public void CeilLog2_uint ()
        {
            for (int i = 0; i < 32; i++)
                Assert.AreEqual (i, BitOps.CeilLog2 (1u << i));

            Assert.AreEqual (32, BitOps.CeilLog2 (UInt32.MaxValue));
        }

        [Test]
        public void CeilLog2_ulong ()
        {
            for (int i = 0; i < 64; i++)
                Assert.AreEqual (i, BitOps.CeilLog2 (1ul << i));

            Assert.AreEqual (64, BitOps.CeilLog2 (UInt64.MaxValue));
        }

        [Test]
        public void CeilLog10_long ()
        {
            for (int i = 0; i < 63; i++)
                Assert.AreEqual ((1L << i).ToString ().Length - 1, BitOps.CeilLog10 ((1L << i)));

            Assert.AreEqual (0, BitOps.CeilLog10 ((1u << 64)));
        }

        [Test]
        public void RoundUpToNextPowerOfTwo ()
        {
            for (int i = 0; i < 31; i++)
                Assert.AreEqual ((1u << (i + 1)), BitOps.RoundUpToPowerOf2 ((1 << i) + 1));

        }
    }
}
