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

using NUnit.Framework;

namespace MonoTorrent.BEncoding
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
        public void DecodeDictionary_OutOfOrder_DefaultIsNotStrict ()
        {
            string benString = "d1:b1:b1:a1:ae";
            var dict = (BEncodedDictionary) BEncodedValue.Decode (Encoding.UTF8.GetBytes (benString));
            Assert.IsTrue (dict.ContainsKey ("a"));
            Assert.IsTrue (dict.ContainsKey ("b"));
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
            Assert.Less (0, new BEncodedString ("").CompareTo ((object) null));
            Assert.AreEqual (0, new BEncodedString ("").CompareTo (new BEncodedString ("")));
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
            Assert.Throws<System.ArgumentNullException> (() => BEncodedString.UrlDecode (null));
            Assert.AreEqual (new BEncodedString (""), BEncodedString.UrlDecode (""));
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
            Assert.AreEqual (new BEncodedString (""), new BEncodedString (""));
            Assert.AreEqual (new BEncodedString (""), new BEncodedString (new char[0]));
            Assert.AreEqual (new BEncodedString (""), (BEncodedString) new char[0]);
            Assert.AreEqual (new BEncodedString (""), BEncodedValue.Decode (Encoding.UTF8.GetBytes ("0:")));
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

        static bool ByteMatch (byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;
            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return false;
            return true;
        }
    }
}
