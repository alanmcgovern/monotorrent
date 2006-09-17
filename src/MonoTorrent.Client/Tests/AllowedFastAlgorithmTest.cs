using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Net;


namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class AllowedFastAlgorithmTest
    {
        [Test]
        public void CalculateTest()
        {
            byte[] infohash = new byte[20];
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("80.4.4.200"), 24142);

            for (int i = 0; i < infohash.Length; i++)
                infohash[i] = (byte)170;

            UInt32[] results = AllowedFastAlgorithm.Calculate(endpoint.Address.GetAddressBytes(), infohash, 9, (UInt32)1313);
            Assert.AreEqual(1059, results[0]);
            Assert.AreEqual(431, results[1]);
            Assert.AreEqual(808, results[2]);
            Assert.AreEqual(1217, results[3]);
            Assert.AreEqual(287, results[4]);
            Assert.AreEqual(376, results[5]);
            Assert.AreEqual(1188, results[6]);
            Assert.AreEqual(353, results[7]);
            Assert.AreEqual(508, results[8]);
        }
    }
}
