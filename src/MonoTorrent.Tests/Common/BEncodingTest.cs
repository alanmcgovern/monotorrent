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



using System;
using System.IO;
using MonoTorrent.Common;
using Xunit;
using System.Text;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{

    /// <summary>
    /// 
    /// </summary>
    
    public class BEncodeTest
    {
        #region Text encoding tests
        [Fact]
        public void UTF8Test()
        {
            string s = "ã";
            BEncodedString str = s;
            Assert.Equal(s, str.Text);
        }

        //[Fact]
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

        //    Assert.Equal(enc8.GetString(utf8Result), enc32.GetString(utf32Result));
        //}
        #endregion


        #region BEncodedString Tests
        [Fact]
        public void benStringDecoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("21:this is a test string");
            using (MemoryStream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.Equal("this is a test string", result.ToString());
                Assert.Equal(result is BEncodedString, true);
                Assert.Equal(((BEncodedString)result).Text, "this is a test string");
            }
        }

        [Fact]
        public void benStringEncoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("22:this is my test string");

            BEncodedString benString = new BEncodedString("this is my test string");
            Assert.True(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Fact]
        public void benStringEncoding2()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("0:");

            BEncodedString benString = new BEncodedString("");
            Assert.True(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Fact]
        public void benStringEncodingBuffered()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("22:this is my test string");

            BEncodedString benString = new BEncodedString("this is my test string");
            byte[] result = new byte[benString.LengthInBytes()];
            benString.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benStringLengthInBytes()
        {
            string text = "thisisateststring";

            BEncodedString str = text;
            int length = text.Length;
            length += text.Length.ToString().Length;
            length++;

            Assert.Equal(length, str.LengthInBytes());
        }

        [Fact]
        public void corruptBenStringDecode()
        {
            string testString = "50:i'm too short";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(System.Text.Encoding.UTF8.GetBytes(testString)));
        }

        [Fact]
        public void corruptBenStringDecode2()
        {
            string s = "d8:completei2671e10:incompletei669e8:intervali1836e12min intervali918e5:peers0:e";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.ASCII.GetBytes(s)));
        }

        #endregion


        #region BEncodedNumber Tests

        [Fact]
        public void benNumberDecoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i12412e");
            using (Stream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.Equal(result is BEncodedNumber, true);
                Assert.Equal(result.ToString(), "12412");
                Assert.Equal(((BEncodedNumber)result).Number, 12412);
            }
        }

        [Fact]
        public void benNumberEncoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding2()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i0e");
            BEncodedNumber number = 0;
            Assert.Equal(3, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding3()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i1230e");
            BEncodedNumber number = 1230;
            Assert.Equal(6, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding4()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i-1230e");
            BEncodedNumber number = -1230;
            Assert.Equal(7, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding5()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i-123e");
            BEncodedNumber number = -123;
            Assert.Equal(6, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding6 ()
        {
            BEncodedNumber a = -123;
            BEncodedNumber b = BEncodedNumber.Decode<BEncodedNumber>(a.Encode());
            Assert.Equal(a.Number, b.Number, "#1");
        }

        [Fact]
        public void benNumberEncodingBuffered()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            byte[] result = new byte[number.LengthInBytes()];
            number.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benNumberLengthInBytes()
        {
            int number = 1635;
            BEncodedNumber num = number;
            Assert.Equal(number.ToString().Length + 2, num.LengthInBytes());
        }

        [Fact]
        public void corruptBenNumberDecode()
        {
            string testString = "i35212";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(System.Text.Encoding.UTF8.GetBytes(testString)));
        }
        #endregion


        #region BEncodedList Tests
        [Fact]
        public void benListDecoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            using (Stream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.Equal(result.ToString(), "l4:test5:tests6:testede");
                Assert.Equal(result is BEncodedList, true);
                BEncodedList list = (BEncodedList)result;

                Assert.Equal(list.Count, 3);
                Assert.Equal(list[0] is BEncodedString, true);
                Assert.Equal(((BEncodedString)list[0]).Text, "test");
                Assert.Equal(((BEncodedString)list[1]).Text, "tests");
                Assert.Equal(((BEncodedString)list[2]).Text, "tested");
            }
        }

        [Fact]
        public void benListEncoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList();
            list.Add(new BEncodedString("test"));
            list.Add(new BEncodedString("tests"));
            list.Add(new BEncodedString("tested"));

            Assert.True(Toolbox.ByteMatch(data, list.Encode()));
        }

        [Fact]
        public void benListEncodingBuffered()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            BEncodedList list = new BEncodedList();
            list.Add(new BEncodedString("test"));
            list.Add(new BEncodedString("tests"));
            list.Add(new BEncodedString("tested"));
            byte[] result = new byte[list.LengthInBytes()];
            list.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benListStackedTest()
        {
            string benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(benString);
            BEncodedList list = (BEncodedList)BEncodedValue.Decode(data);
            string decoded = System.Text.Encoding.UTF8.GetString(list.Encode());
            Assert.Equal(benString, decoded);
        }

        [Fact]
        public void benListLengthInBytes()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            BEncodedList list = (BEncodedList)BEncodedValue.Decode(data);

            Assert.Equal(data.Length, list.LengthInBytes());
        }

        [Fact]
        public void corruptBenListDecode()
        {
            string testString = "l3:3521:a3:ae";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(System.Text.Encoding.UTF8.GetBytes(testString)));
        }
        #endregion


        #region BEncodedDictionary Tests
        [Fact]
        public void benDictionaryDecoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            using (Stream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.Equal(result.ToString(), "d4:spaml1:a1:bee");
                Assert.Equal(result is BEncodedDictionary, true);

                BEncodedDictionary dict = (BEncodedDictionary)result;
                Assert.Equal(dict.Count, 1);
                Assert.True(dict["spam"] is BEncodedList);

                BEncodedList list = (BEncodedList)dict["spam"];
                Assert.Equal(((BEncodedString)list[0]).Text, "a");
                Assert.Equal(((BEncodedString)list[1]).Text, "b");
            }
        }

        [Fact]
        public void benDictionaryEncoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");

            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedList list = new BEncodedList();
            list.Add(new BEncodedString("a"));
            list.Add(new BEncodedString("b"));
            dict.Add("spam", list);
            Assert.Equal(System.Text.Encoding.UTF8.GetString(data), System.Text.Encoding.UTF8.GetString(dict.Encode()));
            Assert.True(Toolbox.ByteMatch(data, dict.Encode()));
        }

        [Fact]
        public void benDictionaryEncodingBuffered()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedList list = new BEncodedList();
            list.Add(new BEncodedString("a"));
            list.Add(new BEncodedString("b"));
            dict.Add("spam", list);
            byte[] result = new byte[dict.LengthInBytes()];
            dict.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benDictionaryStackedTest()
        {
            string benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(benString);
            BEncodedDictionary dict = (BEncodedDictionary)BEncodedValue.Decode(data);
            string decoded = System.Text.Encoding.UTF8.GetString(dict.Encode());
            Assert.Equal(benString, decoded);
        }

        [Fact]
        public void benDictionaryLengthInBytes()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            BEncodedDictionary dict = (BEncodedDictionary)BEncodedValue.Decode(data);

            Assert.Equal(data.Length, dict.LengthInBytes());
        }


        [Fact]
        public void corruptBenDictionaryDecode()
        {
            string testString = "d3:3521:a3:aedddd";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(System.Text.Encoding.UTF8.GetBytes(testString)));
        }
        #endregion


        #region General Tests
        [Fact]
        public void corruptBenDataDecode()
        {
            string testString = "corruption!";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(System.Text.Encoding.UTF8.GetBytes(testString)));
        }
        #endregion
    }
}