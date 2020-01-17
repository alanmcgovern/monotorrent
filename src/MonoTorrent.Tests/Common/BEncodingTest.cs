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
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("21:this is a test string");
            using (MemoryStream stream = new MemoryStream (data)) {
                BEncodedValue result = BEncodedValue.Decode (stream);
                Assert.AreEqual ("this is a test string", result.ToString ());
                Assert.AreEqual (result is BEncodedString, true);
                Assert.AreEqual (((BEncodedString) result).Text, "this is a test string");
            }
        }

        [Test]
        public void benStringEncoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("22:this is my test string");

            BEncodedString benString = new BEncodedString ("this is my test string");
            Assert.IsTrue (Toolbox.ByteMatch (data, benString.Encode ()));
        }

        [Test]
        public void benStringEncoding2 ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("0:");

            BEncodedString benString = new BEncodedString ("");
            Assert.IsTrue (Toolbox.ByteMatch (data, benString.Encode ()));
        }

        [Test]
        public void benStringEncodingBuffered ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("22:this is my test string");

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
                BEncodedValue.Decode (System.Text.Encoding.UTF8.GetBytes (testString));
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
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i12412e");
            using (Stream stream = new MemoryStream (data)) {
                BEncodedValue result = BEncodedValue.Decode (stream);
                Assert.AreEqual (result is BEncodedNumber, true);
                Assert.AreEqual (result.ToString (), "12412");
                Assert.AreEqual (((BEncodedNumber) result).Number, 12412);
            }
        }

        [Test]
        public void benNumberEncoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i12345e");
            BEncodedNumber number = 12345;
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding2 ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i0e");
            BEncodedNumber number = 0;
            Assert.AreEqual (3, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding3 ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i1230e");
            BEncodedNumber number = 1230;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding4 ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i-1230e");
            BEncodedNumber number = -1230;
            Assert.AreEqual (7, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding5 ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i-123e");
            BEncodedNumber number = -123;
            Assert.AreEqual (6, number.LengthInBytes ());
            Assert.IsTrue (Toolbox.ByteMatch (data, number.Encode ()));
        }

        [Test]
        public void benNumberEncoding6 ()
        {
            BEncodedNumber a = -123;
            BEncodedNumber b = BEncodedNumber.Decode<BEncodedNumber> (a.Encode ());
            Assert.AreEqual (a.Number, b.Number, "#1");
        }

        [Test]
        public void benNumberEncodingBuffered ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("i12345e");
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
                BEncodedValue.Decode (System.Text.Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion


        #region BEncodedList Tests
        [Test]
        public void benListDecoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            using (Stream stream = new MemoryStream (data)) {
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
        }

        [Test]
        public void benListEncoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList ();
            list.Add (new BEncodedString ("test"));
            list.Add (new BEncodedString ("tests"));
            list.Add (new BEncodedString ("tested"));

            Assert.IsTrue (Toolbox.ByteMatch (data, list.Encode ()));
        }

        [Test]
        public void benListEncodingBuffered ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList ();
            list.Add (new BEncodedString ("test"));
            list.Add (new BEncodedString ("tests"));
            list.Add (new BEncodedString ("tested"));
            byte[] result = new byte[list.LengthInBytes ()];
            list.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benListStackedTest ()
        {
            string benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";
            byte[] data = System.Text.Encoding.UTF8.GetBytes (benString);
            BEncodedList list = (BEncodedList) BEncodedValue.Decode (data);
            string decoded = System.Text.Encoding.UTF8.GetString (list.Encode ());
            Assert.AreEqual (benString, decoded);
        }

        [Test]
        public void benListLengthInBytes ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("l4:test5:tests6:testede");
            BEncodedList list = (BEncodedList) BEncodedValue.Decode (data);

            Assert.AreEqual (data.Length, list.LengthInBytes ());
        }

        [Test]
        public void corruptBenListDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "l3:3521:a3:ae";
                BEncodedValue.Decode (System.Text.Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion


        #region BEncodedDictionary Tests
        [Test]
        public void benDictionaryDecoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            using (Stream stream = new MemoryStream (data)) {
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
        }

        [Test]
        public void benDictionaryEncoding ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");

            BEncodedDictionary dict = new BEncodedDictionary ();
            BEncodedList list = new BEncodedList ();
            list.Add (new BEncodedString ("a"));
            list.Add (new BEncodedString ("b"));
            dict.Add ("spam", list);
            Assert.AreEqual (System.Text.Encoding.UTF8.GetString (data), System.Text.Encoding.UTF8.GetString (dict.Encode ()));
            Assert.IsTrue (Toolbox.ByteMatch (data, dict.Encode ()));
        }

        [Test]
        public void benDictionaryEncodingBuffered ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            BEncodedDictionary dict = new BEncodedDictionary ();
            BEncodedList list = new BEncodedList ();
            list.Add (new BEncodedString ("a"));
            list.Add (new BEncodedString ("b"));
            dict.Add ("spam", list);
            byte[] result = new byte[dict.LengthInBytes ()];
            dict.Encode (result, 0);
            Assert.IsTrue (Toolbox.ByteMatch (data, result));
        }

        [Test]
        public void benDictionaryStackedTest ()
        {
            string benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";
            byte[] data = System.Text.Encoding.UTF8.GetBytes (benString);
            BEncodedDictionary dict = (BEncodedDictionary) BEncodedValue.Decode (data);
            string decoded = System.Text.Encoding.UTF8.GetString (dict.Encode ());
            Assert.AreEqual (benString, decoded);
        }

        [Test]
        public void benDictionaryLengthInBytes ()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes ("d4:spaml1:a1:bee");
            BEncodedDictionary dict = (BEncodedDictionary) BEncodedValue.Decode (data);

            Assert.AreEqual (data.Length, dict.LengthInBytes ());
        }


        [Test]
        public void corruptBenDictionaryDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "d3:3521:a3:aedddd";
                BEncodedValue.Decode (System.Text.Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion


        #region General Tests
        [Test]
        public void corruptBenDataDecode ()
        {
            Assert.Throws<BEncodingException> (() => {
                string testString = "corruption!";
                BEncodedValue.Decode (System.Text.Encoding.UTF8.GetBytes (testString));
            });
        }
        #endregion
    }
}