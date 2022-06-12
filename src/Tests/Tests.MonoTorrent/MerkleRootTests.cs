using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent
{
    [TestFixture]
    public class MerkleRootTests
    {
        [Test]
        public void Equality ()
        {
            Func<MerkleRoot> allZeros = () => new MerkleRoot (new byte[32]);
            Func<MerkleRoot> allOnes = () => new MerkleRoot (Enumerable.Repeat ((byte) 1, 32).ToArray ());

            Assert.IsTrue (allZeros ().Equals (allZeros ()));
            Assert.IsFalse (allOnes ().Equals (allZeros ()));

            Assert.IsTrue (allOnes () != allZeros ());
            Assert.IsFalse (allOnes () == allZeros ());

            Assert.IsTrue (allOnes () == allOnes ());
            Assert.IsFalse (allOnes () != allOnes ());

            Assert.IsFalse (allZeros () == MerkleRoot.Empty);
            Assert.IsFalse (allOnes () == MerkleRoot.Empty);
            Assert.IsTrue (MerkleRoot.Empty == MerkleRoot.Empty);
        }
    }
}
