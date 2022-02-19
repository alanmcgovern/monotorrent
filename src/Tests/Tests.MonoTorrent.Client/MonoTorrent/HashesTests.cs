using System;
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class HashesTests
    {
        IPieceHashes Hashes { get; set; }

        [SetUp]
        public void Setup ()
        {
            Hashes = new PieceHashesV1 (new byte[20 * 10], 20);
        }

        [Test]
        public void IsValid_DoesNotMatch ()
        {
            var other = new PieceHash (Enumerable.Repeat ((byte) 20, 20).ToArray (), default);
            Assert.IsFalse (Hashes.IsValid (other, 0));
        }

        [Test]
        public void IsValid_InvalidIndex ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.IsValid (new PieceHash (new byte[20], default), -1));
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.IsValid (new PieceHash (new byte[20], default), Hashes.Count));
        }

        [Test]
        public void IsValid_Matches ()
        {
            Assert.IsTrue (Hashes.IsValid (new PieceHash (new byte[20]), 0));
        }

        [Test]
        public void Read_InvalidIndex ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.GetHash (-1));
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.GetHash (Hashes.Count));
        }
    }
}
