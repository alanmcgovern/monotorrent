//
// AllowedFastAlgorithm.cs
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

            List<UInt32> results = AllowedFastAlgorithm.Calculate(endpoint.Address.GetAddressBytes(), infohash, 9, (UInt32)1313);
            Assert.AreEqual(1059, results[0], "#1");
            Assert.AreEqual(431, results[1], "#2");
            Assert.AreEqual(808, results[2], "#3");
            Assert.AreEqual(1217, results[3], "#4");
            Assert.AreEqual(287, results[4], "#5");
            Assert.AreEqual(376, results[5], "#6");
            Assert.AreEqual(1188, results[6], "#7");
            Assert.AreEqual(353, results[7], "#8");
            Assert.AreEqual(508, results[8], "#9");
        }
    }
}
