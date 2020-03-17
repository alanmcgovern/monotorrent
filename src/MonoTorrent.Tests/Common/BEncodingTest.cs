//
// BEncodingTest.cs
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


using System.IO;
using System.Text;

using MonoTorrent.BEncoding;

using NUnit.Framework;

namespace MonoTorrent.Common
{

    /// <summary>
    /// 
    /// </summary>
    [TestFixture]
    public class BEncodeTest
    {
        #region Text encoding tests
        [Test]
        public void UTF8Test ()
        {
            string s = "\u0192";
            BEncodedString str = s;
            Assert.AreEqual (s, str.Text);
        }

        //[Test]
        //public void EncodingUTF32()
        //{
        //    UTF8Encoding enc8 = new UTF8Encoding();
        //    UTF32Encoding enc32 = new UTF32Encoding();
        //    BEncodedDictionary val = new BEncodedDictionary();

        //    val.Add("Test", (BEncodedNumber)1532);
        //    val.Add("yeah", (BEncodedString)"whoop");
        //    val.Add("mylist", new BEncodedList());
        //    val.Add("mydict", new BEncodedDictionary());

        //    byte[] utf8Result = val.Encode();
        //    byte[] utf32Result = val.Encode(enc32);

        //    Assert.AreEqual(enc8.GetString(utf8Result), enc32.GetString(utf32Result));
        //}
        #endregion


        #region BEncodedString Tests
        [Test]
        public void benStringDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("21:this is a test string");
            using MemoryStream stream = new MemoryStream (data);
            BEncodedValue result = BEncodedValue.Decode (stream);
            Assert.AreEqual ("this is a test string", result.ToString ());
            Assert.AreEqual (result is BEncodedString, true);
            Assert.AreEqual (((BEncodedString) result).Text, "this is a test string");
        }

        [Test]
        public void benStringEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("22:this is my test string");

            BEncodedString benString = new BEncodedString ("this is my test string");
            Assert.IsTrue (Toolbox.ByteMatch (data, benString.Encode ()));
        }

        [Test]
        public void benStringEncoding2 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("0:");

            BEncodedString benString = new BEncodedString ("");
            Assert.IsTrue (Toolbox.ByteMatch (data, benString.Encode ()));
        }

        [Test]
        public void benStringEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("22:this is my test string");

            BEncodedString benString = new BEncodedString ("this is my test string");
            byte[] result = new byte[benString.LengthInBytes ()];
            benString.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benStringLengthInBytes ()
        {
            string text = "thisisateststring";

            BEncodedString str = text;
            int length = text.Length;
            length += text.Length.ToString ().Length;
            length++;

            Assert.AreEqual (length, str.LengthInBytes ());
        }

        [Test]
        public void corruptBenStringDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "50:i'm too short";
                BEncodedValue.Decode (Encoding.UTF8.GetBytes (testString));
            });
        }

        [Test]
        public void corruptBenStringDecode2 ()
        {
            Assert.Throws<BEncodingException> (() => {
                string s = "d8:completei2671e10:incompletei669e8:intervali1836e12min intervali918e5:peers0:e";
                BEncodedValue.Decode (Encoding.ASCII.GetBytes (s));
            });
        }

        #endregion


        #region BEncodedNumber Tests

        [Test]
        public void benNumberDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i12412e");
            using Stream stream = new MemoryStream (data);
            BEncodedValue result = BEncodedValue.Decode (stream);
            Assert.AreEqual (result is BEncodedNumber, true);
            Assert.AreEqual (result.ToString (), "12412");
            Assert.AreEqual (((BEncodedNumber) result).Number, 12412);
        }

        [Test]
        public void benNumberEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i12345e");
            BEncodedNumber number = 12345;
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding2 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i0e");
            BEncodedNumber number = 0;
            Assert.AreEqual (3, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding3 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i1230e");
            BEncodedNumber number = 1230;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding4 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i-1230e");
            BEncodedNumber number = -1230;
            Assert.AreEqual (7, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding5 ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i-123e");
            BEncodedNumber number = -123;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding6 ()
        {
            BEncodedNumber a = -123;
            BEncodedNumber b = BEncodedValue.Decode<BEncodedNumber> (a.Encode ());
            Assert.AreEqual (a.Number, b.Number, "#1");
        }

        [Test]
        public void benNumber_MaxMin ([Values (long.MinValue, long.MaxValue)] long value)
        {
            var number = new BEncodedNumber (value);
            var result = BEncodedValue.Decode<BEncodedNumber> (number.Encode ());
            Assert.AreEqual (result.Number, value);
        }

        [Test]
        public void benNumberEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("i12345e");
            BEncodedNumber number = 12345;
            byte[] result = new byte[number.LengthInBytes ()];
            number.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benNumberLengthInBytes ()
        {
            int number = 1635;
            BEncodedNumber num = number;
            Assert.AreEqual (number.ToString ().Length + 2, num.LengthInBytes ());
        }

        [Test]
        public void corruptBenNumberDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "i35212";
                BEncodedValue.Decode (Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion


        #region BEncodedList Tests
        [Test]
        public void benListDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            using Stream stream = new MemoryStream (data);
            BEncodedValue result = BEncodedValue.Decode (stream);
            Assert.AreEqual (result.ToString (), "l4:test5:tests6:testede");
            Assert.AreEqual (result is BEncodedList, true);
            BEncodedList list = (BEncodedList) result;

            Assert.AreEqual (list.Count, 3);
            Assert.AreEqual (list[0] is BEncodedString, true);
            Assert.AreEqual (((BEncodedString) list[0]).Text, "test");
            Assert.AreEqual (((BEncodedString) list[1]).Text, "tests");
            Assert.AreEqual (((BEncodedString) list[2]).Text, "tested");
        }

        [Test]
        public void benListEncoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList {
                new BEncodedString ("test"),
                new BEncodedString ("tests"),
                new BEncodedString ("tested")
            };

            Assert.IsTrue (Toolbox.ByteMatch (data, list.Encode ()));
        }

        [Test]
        public void benListEncodingBuffered ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList {
                new BEncodedString ("test"),
                new BEncodedString ("tests"),
                new BEncodedString ("tested")
            };
            byte[] result = new byte[list.LengthInBytes ()];
            list.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benListStackedTest ()
        {
            string benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";
            byte[] data = Encoding.UTF8.GetBytes (benString);
            BEncodedList list = (BEncodedList) BEncodedValue.Decode (data);
            string decoded = Encoding.UTF8.GetString (list.Encode ());
            Assert.AreEqual (benString, decoded);
        }

        [Test]
        public void benListLengthInBytes ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = (BEncodedList) BEncodedValue.Decode (data);

            Assert.AreEqual (data.Length, list.LengthInBytes ());
        }

        [Test]
        public void corruptBenListDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "l3:3521:a3:ae";
                BEncodedValue.Decode (Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion


        #region BEncodedDictionary Tests
        [Test]
        public void benDictionaryDecoding ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            using Stream stream = new MemoryStream (data);
            BEncodedValue result = BEncodedValue.Decode (stream);
            Assert.AreEqual (result.ToString (), "d4:spaml1:a1:bee");
            Assert.AreEqual (result is BEncodedDictionary, true);

            BEncodedDictionary dict = (BEncodedDictionary) result;
            Assert.AreEqual (dict.Count, 1);
            Assert.IsTrue (dict["spam"] is BEncodedList);

            BEncodedList list = (BEncodedList) dict["spam"];
            Assert.AreEqual (((BEncodedString) list[0]).Text, "a");
            Assert.AreEqual (((BEncodedString) list[1]).Text, "b");
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
            Assert.IsTrue (Toolbox.ByteMatch (data, dict.Encode ()));
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
            dict.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benDictionaryStackedTest ()
        {
            string benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";
            byte[] data = Encoding.UTF8.GetBytes (benString);
            BEncodedDictionary dict = (BEncodedDictionary) BEncodedValue.Decode (data);
            string decoded = Encoding.UTF8.GetString (dict.Encode ());
            Assert.AreEqual (benString, decoded);
        }

        [Test]
        public void benDictionaryLengthInBytes ()
        {
            byte[] data = Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            BEncodedDictionary dict = (BEncodedDictionary) BEncodedValue.Decode (data);

            Assert.AreEqual (data.Length, dict.LengthInBytes ());
        }


        [Test]
        public void corruptBenDictionaryDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "d3:3521:a3:aedddd";
                BEncodedValue.Decode (Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion

        [Test]
        public void corruptBenDataDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "corruption!";
                BEncodedValue.Decode (Encoding.UTF8.GetBytes (testString));
            });
        }

        [Test]
        public void DecodeDictionary_MissingTrailingE ()
        {
            string benString = "d1:a1:b";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void DecodeDictionary_OutOfOrder_NotStrict ()
        {
            string benString = "d1:b1:b1:a1:ae";
            var dict = (BEncodedDictionary) BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString), false);
            Assert.IsTrue (dict.ContainsKey ("a"));
            Assert.IsTrue (dict.ContainsKey ("b"));
        }

        [Test]
        public void DecodeDictionary_OutOfOrder_Strict ()
        {
            string benString = "d1:b1:b1:a1:ae";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString), true));
        }

        [Test]
        public void DecodeList_MissingTrailingE ()
        {
            string benString = "l1:a";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void DecodeNumber_InvalidDigit ()
        {
            string benString = "i123$21e";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void BEncodedString_Compare ()
        {
            Assert.Less (0, new BEncodedString ().CompareTo ((object) null));
            Assert.AreEqual (0, new BEncodedString ().CompareTo (new BEncodedString ()));
            Assert.AreEqual (0, new BEncodedString ("a").CompareTo (new BEncodedString ("a")));
        }

        [Test]
        public void BEncodedString_Equals ()
        {
            Assert.IsFalse (new BEncodedString ("test").Equals (null));
            Assert.IsFalse (new BEncodedString ("test").Equals ("tesT"));
            Assert.IsTrue (new BEncodedString ("test").Equals ("test"));
        }

        [Test]
        public void BEncodedString_FromUrlEncodedString ()
        {
            Assert.AreEqual (null, BEncodedString.FromUrlEncodedString (null));
            Assert.AreEqual (new BEncodedString (), BEncodedString.FromUrlEncodedString (""));
        }

        [Test]
        public void BEncodedString_ImplicitConversions ()
        {
            Assert.AreEqual (null, (BEncodedString) (string) null);
            Assert.AreSame (BEncodedString.Empty, (BEncodedString) "");
            Assert.AreEqual (new BEncodedString ("teststr"), (BEncodedString) "teststr");
        }

        [Test]
        public void BEncodedString_THex ()
        {
            byte[] bytes = { 1, 2, 3, 4, 5 };
            Assert.AreEqual (System.BitConverter.ToString (bytes), new BEncodedString (bytes).ToHex ());
        }

        [Test]
        public void DecodeString_Empty ()
        {
            Assert.AreEqual (new BEncodedString (), new BEncodedString (""));
            Assert.AreEqual (new BEncodedString (), new BEncodedString (new char[0]));
            Assert.AreEqual (new BEncodedString (), (BEncodedString) new char[0]);
            Assert.AreEqual (new BEncodedString (), BEncodedValue.Decode (Encoding.UTF8.GetBytes ("0:")));
        }

        [Test]
        public void DecodeString_NoColon ()
        {
            string benString = "12";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString)));
        }

        [Test]
        public void DecodeString_TooShort ()
        {
            string benString = "5:test";
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString)));
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
        public void DecodeTorrentWithDict ()
        {
            var dict = new BEncodedDictionary {
                { "other", new BEncodedDictionary   { { "test", new BEncodedString ("value") } } }
            };

            var result = BEncodedDictionary.DecodeTorrent (dict.Encode ());
            Assert.IsTrue (Toolbox.ByteMatch (dict.Encode (), result.Encode ()));
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
            Assert.IsTrue (Toolbox.ByteMatch (dict.Encode (), result.Encode ()));
        }


        [Test]
        public void DecodeTorrentWithString ()
        {
            var dict = new BEncodedDictionary {
                { "info", (BEncodedString) "value" }
            };

            var result = BEncodedDictionary.DecodeTorrent (dict.Encode ());
            Assert.IsTrue (Toolbox.ByteMatch (dict.Encode (), result.Encode ()));
        }

    }
}