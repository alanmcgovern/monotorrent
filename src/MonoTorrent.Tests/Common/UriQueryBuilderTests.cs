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
using Xunit;

namespace MonoTorrent.Common
{

    public class UriQueryBuilderTest
	{

		[Fact]
        public void TestToString ()
        {
            UriQueryBuilder bld = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1");
            bld.Add ("key", 2);
            bld.Add ("foo", 2);
            bld.Add ("foo", "bar");
            Assert.Equal(new Uri ("http://mytest.com/announce.aspx?key=2&foo=bar"), bld.ToUri ());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx?passkey=1");
            bld.Add ("key", 2);
            Assert.Equal(new Uri ("http://mytest.com/announce.aspx?passkey=1&key=2"), bld.ToUri ());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx");
            Assert.Equal(new Uri ("http://mytest.com/announce.aspx"), bld.ToUri ());

            bld = new UriQueryBuilder("http://mytest.com/announce.aspx");
            byte[] infoHash = new byte[6] {0x01, 0x47, 0xff, 0xaa, 0xbb, 0xcc};
            bld.Add ("key", UriHelper.UrlEncode(infoHash));
            Assert.Equal(new Uri ("http://mytest.com/announce.aspx?key=%01G%ff%aa%bb%cc"), bld.ToUri ());


        }
        
        [Fact]
        public void ContainQuery ()
        {
            UriQueryBuilder bld = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1&foo=bar");
            Assert.True(bld.Contains ("key"));
            Assert.True(bld.Contains ("foo"));
            Assert.False(bld.Contains ("bar"));
        }

        [Fact]
        public void CaseInsensitiveTest ()
        {
            UriQueryBuilder b = new UriQueryBuilder ("http://www.example.com?first=1&second=2&third=4");
            Assert.True (b.Contains ("FiRsT"));
            Assert.Equal (b ["FiRst"], "1");
        }

        [Fact]
        public void AddParams ()
        {
            UriQueryBuilder b = new UriQueryBuilder ("http://example.com");
            b ["Test"] = "2";
            b ["Test"] = "7";
            Assert.Equal ("7", b ["Test"]);
        }
	}
}