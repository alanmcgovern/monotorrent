using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using MonoTorrent;

namespace MonoTorrent.BEncoding
{
    [TestFixture]
    public class MemoryExtensionsTests
    {
        [Test]
        public void HashLargeBuffer ()
        {
            var buffer = new byte[12345];
            new Random ().NextBytes (buffer);

            var hash = SHA1.Create ().ComputeHash (buffer);

            byte[] destination = new byte[20];
            SHA1.Create ().TryComputeHash (buffer, destination, out int written);
            Assert.AreEqual (20, written);
            Assert.IsTrue (hash.AsSpan ().SequenceEqual (destination.AsSpan ()));
        }

        [Test]
        public void IncrementalHashLargeBuffer ()
        {
            var buffer = new byte[12345];
            new Random ().NextBytes (buffer);

            var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA1);
            hasher.AppendData (buffer, 0, 11345);
            var hash = hasher.GetHashAndReset ();

            hasher.AppendData (new Memory<byte> (buffer).Span.Slice (0, 11345));
            byte[] destination = hasher.GetHashAndReset ();

            Assert.IsTrue (hash.AsSpan ().SequenceEqual (destination.AsSpan ()));

        }
    }
}
