//
// BEncodedDictionaryTests.cs
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
using System.IO;
using System.Text;

using NUnit.Framework;

namespace MonoTorrent.BEncoding
{
    [TestFixture]
    public class BEncodedDictionaryTests
    {
        [Test]
        public void benDictionaryDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            foreach (var dict in BEncodedValue.DecodingVariants<BEncodedDictionary> (data)) {
                Assert.AreEqual (dict.Count, 1);
                Assert.IsTrue (dict["spam"] is BEncodedList);

                BEncodedList list = (BEncodedList) dict["spam"];
                Assert.AreEqual (((BEncodedString) list[0]).Text, "a");
                Assert.AreEqual (((BEncodedString) list[1]).Text, "b");
            }
        }

        [Test]
        public void benDictionaryEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");

            var dict = new BEncodedDictionary ();
            var list = new BEncodedList {
                new BEncodedString ("a"),
                new BEncodedString ("b")
            };
            dict.Add ("spam", list);
            Assert.AreEqual (Encoding.UTF8.GetString (data), Encoding.UTF8.GetString (dict.Encode ()));
            Assert.IsTrue (data.AsSpan ().SequenceEqual (dict.Encode ()));
        }

        [Test]
        public void benDictionaryEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            var dict = new BEncodedDictionary ();
            var list = new BEncodedList {
                new BEncodedString ("a"),
                new BEncodedString ("b")
            };
            dict.Add ("spam", list);
            byte[] result = new byte[dict.LengthInBytes ()];
            dict.Encode (result);
            Assert.IsTrue (data.AsSpan ().SequenceEqual (result));
        }

        [Test]
        public void benDictionaryStackedTest ()
        {
            string benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";
            byte[] data = Encoding.UTF8.GetBytes (benString);
            foreach (var dict in BEncodedValue.DecodingVariants<BEncodedDictionary> (data)) {
                string decoded = Encoding.UTF8.GetString (dict.Encode ());
                Assert.AreEqual (benString, decoded);
            }
        }

        [Test]
        public void benDictionaryLengthInBytes ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            foreach (var dict in BEncodedValue.DecodingVariants<BEncodedDictionary> (data))
                Assert.AreEqual (data.Length, dict.LengthInBytes ());
        }

        [Test]
        public void corruptBenDictionaryDecode ()
        {
            var data = Encoding.UTF8.GetBytes ("d3:3521:a3:aedddd");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }

        [Test]
        public void Decode_CorruptKeyLength ()
        {
            var data = Encoding.UTF8.GetBytes ("da1:3521:a3:aedddd");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }

        [Test]
        public void DecodeTorrent_CorruptKeyLength ()
        {
            var data = Encoding.UTF8.GetBytes ("da1:3521:a3:aedddd");
            Assert.Throws<BEncodingException> (() => BEncodedDictionary.DecodeTorrent (data));
            Assert.Throws<BEncodingException> (() => BEncodedDictionary.DecodeTorrent (new MemoryStream (data)));
        }


        [Test]
        public void DecodeTorrentNotDictionary ()
        {
            string benString = "5:test";
            Assert.Throws<BEncodingException> (() => BEncodedDictionary.DecodeTorrent (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void DecodeTorrent_MissingTrailingE ()
        {
            string benString = "d1:a1:b";
            Assert.Throws<BEncodingException> (() => BEncodedDictionary.DecodeTorrent (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void DecodeTorrentWithOutOfOrderKeys ()
        {
            var good = "d4:infod5:key_a7:value_a5:key_b7:value_bee";
            var bad = "d4:infod5:key_b7:value_b5:key_a7:value_aee";

            var good_result = BEncodedDictionary.DecodeTorrent (Encoding.UTF8.GetBytes (good));
            var bad_result = BEncodedDictionary.DecodeTorrent (Encoding.UTF8.GetBytes (bad));
            Assert.IsTrue (good_result.torrent.Encode ().AsSpan ().SequenceEqual (bad_result.torrent.Encode ()));
            Assert.AreNotEqual (good_result.infoHashes.SHA1, bad_result.infoHashes.SHA1);
        }

        [Test]
        public void DecodeTorrentWithInfo ()
        {
            var infoDict = new BEncodedDictionary {
                { "test", new BEncodedString ("value") }
            };
            var dict = new BEncodedDictionary {
                { "info", infoDict }
            };

            var result = BEncodedDictionary.DecodeTorrent (dict.Encode ());
            Assert.IsTrue (dict.Encode ().AsSpan ().SequenceEqual (result.torrent.Encode ()));
        }


        [Test]
        public void DecodeTorrentWithString ()
        {
            var dict = new BEncodedDictionary {
                { "info", (BEncodedString) "value" }
            };

            var result = BEncodedDictionary.DecodeTorrent (dict.Encode ());
            Assert.IsTrue (dict.Encode ().AsSpan ().SequenceEqual (result.torrent.Encode ()));
        }

        [Test]
        public void DecodeTorrent_CompareSpanAndStream ()
        {
            var data = Encoding.UTF8.GetBytes ("d4:infod5:key_a7:value_a5:key_b7:value_bee");
            var span = BEncodedDictionary.DecodeTorrent (data);
            var stream = BEncodedDictionary.DecodeTorrent (new MemoryStream (data));
            Assert.IsTrue (span.torrent.Encode ().AsSpan ().SequenceEqual (stream.torrent.Encode ()));
            Assert.IsTrue (span.infoHashes.SHA1.Span.SequenceEqual (stream.infohashes.SHA1.Span));
            Assert.IsTrue (span.infoHashes.SHA256.Span.SequenceEqual (stream.infohashes.SHA256.Span));
        }
    }
}
