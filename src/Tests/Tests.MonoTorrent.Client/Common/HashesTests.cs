using System;
using System.Linq;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class HashesTests
    {
        Hashes Hashes { get; set; }

        [SetUp]
        public void Setup ()
        {
            Hashes = new Hashes (new byte[20 * 10], 10);
        }

        [Test]
        public void IsValid_DoesNotMatch ()
        {
            var other = Enumerable.Repeat ((byte) 20, 20).ToArray ();
            Assert.IsFalse (Hashes.IsValid (other, 0));
        }

        [Test]
        public void IsValid_InvalidIndex ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.IsValid (new byte[20], -1));
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.IsValid (new byte[20], Hashes.Count));
        }

        [Test]
        public void IsValid_Matches ()
        {
            Assert.IsTrue (Hashes.IsValid (new byte[20], 0));
        }

        [Test]
        public void IsValid_Null ()
        {
            Assert.Throws<ArgumentNullException> (() => Hashes.IsValid (null, 0));
        }

        [Test]
        public void IsValid_WrongLength ()
        {
            Assert.Throws<ArgumentException> (() => Hashes.IsValid (new byte[5], 0));

        }

        [Test]
        public void Read_InvalidIndex ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.ReadHash (-1));
            Assert.Throws<ArgumentOutOfRangeException> (() => Hashes.ReadHash (Hashes.Count));
        }
    }
}
