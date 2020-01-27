//
// MagnetLinkTest.cs
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

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class MagnetLinkTest
    {
        InfoHash Create ()
        {
            return new InfoHash (new byte[] {
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20
            });
        }

        [Test]
        public void ExactLength ()
        {
            var link = MagnetLink.Parse ("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C&xl=12345");
            Assert.AreEqual (12345, link.Size, "#1");

            link = MagnetLink.Parse ("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C");
            Assert.IsNull (link.Size, "#2");
        }

        [Test]
        public void InfoHashTest ()
        {
            MagnetLink link = MagnetLink.Parse ("magnet:?xt.1=urn:sha1:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C");
            Assert.AreEqual (InfoHash.FromBase32 ("YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C"), link.InfoHash, "#1");

            //base32
            InfoHash initial = new InfoHash (System.Text.Encoding.ASCII.GetBytes ("foobafoobafoobafooba"));
            link = MagnetLink.Parse ("magnet:?xt=urn:sha1:MZXW6YTBMZXW6YTBMZXW6YTBMZXW6YTB");
            Assert.AreEqual (initial, link.InfoHash, "#2");

            //base40 = hex
            InfoHash hash = Create ();
            string hex = hash.ToHex ();
            link = MagnetLink.Parse ($"magnet:?xt=urn:btih:{hex}");
            Assert.AreEqual (hash, link.InfoHash, "#3");
        }

        [Test]
        public void ParseMagnetLink ()
        {
            InfoHash hash = Create ();
            string magnet = $"magnet:?xt=urn:btih:{hash.ToHex ()}";
            MagnetLink other = MagnetLink.Parse (magnet);
            Assert.AreEqual (hash, other.InfoHash, "#1");
        }

        [Test]
        public void InvalidMagnetLink ()
        {
            Assert.Throws<UriFormatException> (() => {
                InfoHash hash = Create ();
                string magnet = $"magnet?xt=urn:btih:{hash.ToHex ()}";
                MagnetLink other = MagnetLink.Parse (magnet);
                Assert.AreEqual (hash, other.InfoHash, "#1");
            });
        }

        [Test]
        public void InvalidMagnetLink3 ()
        {
            Assert.Throws<FormatException> (() => {
                InfoHash hash = Create ();
                string magnet = string.Format ("magnet:?xt=urn:btih:", hash.ToHex ());
                MagnetLink other = MagnetLink.Parse (magnet);
                Assert.AreEqual (hash, other.InfoHash, "#1");
            });
        }

        [Test]
        public void InvalidMagnetLink4 ()
        {
            Assert.Throws<FormatException> (() => {
                InfoHash hash = Create ();
                string magnet = string.Format ("magnet:?xt=urn:btih:23526246235623564234365879634581726345981", hash.ToHex ());
                MagnetLink other = MagnetLink.Parse (magnet);
                Assert.AreEqual (hash, other.InfoHash, "#1");
            });
        }

        [Test]
        public void InvalidMagnetLink_DoubleEquals ()
        {
            Assert.Throws<FormatException> (() => MagnetLink.FromUri (new Uri ("magnet://btih:?xt=urn=:btih:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C")));
        }

        [Test]
        public void NullMagnetLink ()
        {
            Assert.Throws<ArgumentNullException> (() => new MagnetLink (null));
        }

        [Test]
        public void TrackersTest ()
        {
            MagnetLink other = MagnetLink.Parse ($"magnet:?tr=http://example.com/announce&tr.2=http://example.com/announce2&xt=urn:btih:{Create ().ToHex ()}");
            Assert.IsNotNull (other.AnnounceUrls, "#1");
            Assert.IsTrue (other.AnnounceUrls.Contains ("http://example.com/announce"), "#2");
            Assert.IsTrue (other.AnnounceUrls.Contains ("http://example.com/announce2"), "#3");
        }

        [Test]
        public void NameTest ()
        {
            MagnetLink other = MagnetLink.Parse ($"magnet:?dn=foo&xt=urn:btih:{Create ().ToHex ()}");
            Assert.IsNotNull (other.Name, "#1");
            Assert.AreEqual ("foo", other.Name, "#2");
        }

        [Test]
        public void TrackersUrlEncodedTest ()
        {
            var other = MagnetLink.Parse ("magnet:?xt=urn:ed2k:354B15E68FB8F36D7CD88FF94116CDC1" +
                        "&xl=10826029&dn=mediawiki-1.15.1.tar.gz" +
                        "&xt=urn:tree:tiger:7N5OAMRNGMSSEUE3ORHOKWN4WWIQ5X4EBOOTLJY" +
                        "&xt=urn:btih:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C" +
                        "&tr=http%3A%2F%2Ftracker.example.org%2Fannounce.php%3Fuk%3D1111111111%26" +
                        "&tr=udp%3A%2F%2Fexample.org%3A28191" +
                        "&tr=tcp%3A%2F%2F%5B2001%3Adb8%3A85a3%3A8d3%3A1319%3A8a2e%3A370%3A7348%5D");
            Assert.IsNotNull (other.AnnounceUrls, "#1");
            Assert.IsTrue (other.AnnounceUrls.Contains ("http://tracker.example.org/announce.php?uk=1111111111&"), "#2");
            Assert.IsTrue (other.AnnounceUrls.Contains ("udp://example.org:28191"), "#3");
            Assert.IsTrue (other.AnnounceUrls.Contains ("tcp://[2001:db8:85a3:8d3:1319:8a2e:370:7348]"), "#4");
        }

        [Test]
        public void ToUriV1 ()
        {
            var infoHash = Create ();
            var name = "MyName/Url/Encoded";
            var announces = new[] {
                "http://testUrl.com/foo/bar/announce",
                "http://test2Url.com/foo/bar",
            };
            var webSeeds = new[] {
                "http://whatever.com/file.exe",
                "http://whatever2.com/file.exe",
            };
            var magnetLink = new MagnetLink (infoHash, name, announces, webSeeds);
            var uriString = magnetLink.ToV1String ();

            Assert.IsFalse (uriString.Contains (announces[0]), "The uri should be Uri encoded");
            Assert.IsFalse (uriString.Contains (webSeeds[0]), "The uri should be Uri encoded");
            Assert.IsFalse (uriString.Contains (name), "The name should be Uri encoded");

            magnetLink = MagnetLink.Parse (uriString);
            Assert.AreEqual (infoHash, magnetLink.InfoHash, "#1");
            Assert.AreEqual (name, magnetLink.Name, "#2");
            CollectionAssert.AreEquivalent (announces, magnetLink.AnnounceUrls, "#3");
            CollectionAssert.AreEquivalent (webSeeds, magnetLink.Webseeds, "#4");

            Assert.AreEqual (magnetLink.ToV1String (), MagnetLink.FromUri (magnetLink.ToV1Uri ()).ToV1String (), "#5");
        }

        [Test]
        public void TwoInfoHashes ()
        {
            Assert.Throws<FormatException> (() => MagnetLink.FromUri (new Uri ("magnet://btih:?foo=bar&xt=urn:btih:YNCKHTQCWBTRNJIV4WNAE52SJUQCZO5C&xt=urn:btih:ANCKHTQCWBTRNJIV4WNAE52SJUQCZO5C")));
        }

        [Test]
        public void UnsupportedScheme ()
        {
            Assert.Throws<FormatException> (() => MagnetLink.FromUri (new Uri ("http://not_a_magnet_link.com")));
        }
    }
}
