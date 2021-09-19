using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.BEncoding
{
    [TestFixture]
    public class BEncodedStringTests
    {
        [Test]
        public void UrlEncodeString()
        {
            var data = Enumerable.Range (0, 20).Select (v => (byte)v).ToArray ();
            var value = new BEncodedString (data);
            var encoded = value.UrlEncode ();
            var decoded = BEncodedString.UrlDecode (encoded);
            CollectionAssert.AreEqual (data, decoded.TextBytes);
        }
    }
}
