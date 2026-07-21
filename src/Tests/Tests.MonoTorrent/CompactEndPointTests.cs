using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent;

using NUnit.Framework;

namespace Tests.MonoTorrent
{
    [TestFixture]
    public class CompactEndPointTests
    {
        [Test]
        public void FromIPAddress ()
        {
            var endpoint = new CompactEndPoint (IPAddress.Parse ("127.0.1.2"), 16);
            Span<byte> dest = new byte[20];
            Assert.IsTrue (endpoint.TryWriteBytes (dest, out int written));
            Assert.AreEqual (6, written);
            Assert.AreEqual (127, dest[0]);
            Assert.AreEqual (0, dest[1]);
            Assert.AreEqual (1, dest[2]);
            Assert.AreEqual (2, dest[3]);
            Assert.AreEqual (0, dest[4]);
            Assert.AreEqual (16, dest[5]);
        }

        [Test]
        public void FromSocketAddressIPv4 ()
        {
            var socketAddress = new IPEndPoint (IPAddress.Parse ("127.0.1.2"), 16).Serialize ();
            var c = new CompactEndPoint (socketAddress);

            Span<byte> dest = new byte[6];
            Assert.IsTrue (c.TryWriteBytes (dest, out int written));
            Assert.AreEqual (6, written);
            Assert.AreEqual (127, dest[0]);
            Assert.AreEqual (0, dest[1]);
            Assert.AreEqual (1, dest[2]);
            Assert.AreEqual (2, dest[3]);
            Assert.AreEqual (0, dest[4]);
            Assert.AreEqual (16, dest[5]);
        }

        [Test]
        public void FromSocketAddressIPv6 ()
        {
            var socketAddress = new IPEndPoint (IPAddress.Parse ("0102:0304:0506:0708:090A:0B0C:0D0E:0F10"), 17).Serialize ();
            var c = new CompactEndPoint (socketAddress);

            Span<byte> dest = new byte[18];
            Assert.IsTrue (c.TryWriteBytes (dest, out int written));
            Assert.AreEqual (18, written);
            Assert.AreEqual (1, dest[0]);
            Assert.AreEqual (2, dest[1]);
            Assert.AreEqual (3, dest[2]);
            Assert.AreEqual (4, dest[3]);
            Assert.AreEqual (5, dest[4]);
            Assert.AreEqual (6, dest[5]);
            Assert.AreEqual (7, dest[6]);
            Assert.AreEqual (8, dest[7]);
            Assert.AreEqual (9, dest[8]);
            Assert.AreEqual (10, dest[9]);
            Assert.AreEqual (11, dest[10]);
            Assert.AreEqual (12, dest[11]);
            Assert.AreEqual (13, dest[12]);
            Assert.AreEqual (14, dest[13]);
            Assert.AreEqual (15, dest[14]);
            Assert.AreEqual (16, dest[15]);
            Assert.AreEqual (0, dest[16]);
            Assert.AreEqual (17, dest[17]);
        }
    }
}
