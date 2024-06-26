//
// UriQueryBuilderTests.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2009 Olivier Dufour
//                    Alan McGovern
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
using System.Linq;
using System.Web;

using NUnit.Framework;

namespace MonoTorrent.Trackers
{
    [TestFixture]
    public class UriQueryBuilderTest
    {

        [Test]
        public void TestToString ()
        {
            UriQueryBuilder bld = new UriQueryBuilder ("http://mytest.com/announce.aspx?key=1");
            bld.Add ("key", 2);
            bld.Add ("foo", 2);
            bld.Add ("foo", "bar");
            Assert.AreEqual (new Uri ("http://mytest.com/announce.aspx?key=2&foo=bar"), bld.ToUri (), "#1");

            bld = new UriQueryBuilder ("http://mytest.com/announce.aspx?passkey=1");
            bld.Add ("key", 2);
            Assert.AreEqual (new Uri ("http://mytest.com/announce.aspx?passkey=1&key=2"), bld.ToUri (), "#2");

            bld = new UriQueryBuilder ("http://mytest.com/announce.aspx");
            Assert.AreEqual (new Uri ("http://mytest.com/announce.aspx"), bld.ToUri (), "#3");

            bld = new UriQueryBuilder ("http://mytest.com/announce.aspx");
            var infoHash = new InfoHash (new byte[] { 0x01, 0x47, 0xff, 0xaa, 0xbb, 0xcc, (byte) ' ' }.Concat (new byte[13]).ToArray ());
            bld.Add ("key", infoHash.UrlEncode ());
            Assert.AreEqual (new Uri ("http://mytest.com/announce.aspx?key=%01G%ff%aa%bb%cc%20%00%00%00%00%00%00%00%00%00%00%00%00%00"), bld.ToUri (), "#4");
        }

        [Test]
        public void ContainQuery ()
        {
            UriQueryBuilder bld = new UriQueryBuilder ("http://mytest.com/announce.aspx?key=1&foo=bar");
            Assert.IsTrue (bld.Contains ("key"), "#1");
            Assert.IsTrue (bld.Contains ("foo"), "#2");
            Assert.IsFalse (bld.Contains ("bar"), "#3");
        }

        [Test]
        public void CaseInsensitiveTest ()
        {
            UriQueryBuilder b = new UriQueryBuilder ("http://www.example.com?first=1&second=2&third=4");
            Assert.IsTrue (b.Contains ("FiRsT"));
            Assert.AreEqual (b["FiRst"], "1");
        }

        [Test]
        public void AddParams ()
        {
            UriQueryBuilder b = new UriQueryBuilder ("http://example.com");
            b["Test"] = "2";
            b["Test"] = "7";
            Assert.AreEqual ("7", b["Test"], "#1");
        }
    }
}
