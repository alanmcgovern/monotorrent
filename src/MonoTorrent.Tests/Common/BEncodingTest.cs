using System.IO;
using System.Text;
using MonoTorrent.BEncoding;
using Xunit;

namespace MonoTorrent.Common
{
    /// <summary>
    /// </summary>
    public class BEncodeTest
    {
        [Fact]
        public void benDictionaryDecoding()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            using (Stream stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.Equal(result.ToString(), "d4:spaml1:a1:bee");
                Assert.Equal(result is BEncodedDictionary, true);

                var dict = (BEncodedDictionary) result;
                Assert.Equal(dict.Count, 1);
                Assert.True(dict["spam"] is BEncodedList);

                var list = (BEncodedList) dict["spam"];
                Assert.Equal(((BEncodedString) list[0]).Text, "a");
                Assert.Equal(((BEncodedString) list[1]).Text, "b");
            }
        }

        [Fact]
        public void benDictionaryEncoding()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");

            var dict = new BEncodedDictionary();
            var list = new BEncodedList();
            list.Add(new BEncodedString("a"));
            list.Add(new BEncodedString("b"));
            dict.Add("spam", list);
            Assert.Equal(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(dict.Encode()));
            Assert.True(Toolbox.ByteMatch(data, dict.Encode()));
        }

        [Fact]
        public void benDictionaryEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            var dict = new BEncodedDictionary();
            var list = new BEncodedList();
            list.Add(new BEncodedString("a"));
            list.Add(new BEncodedString("b"));
            dict.Add("spam", list);
            var result = new byte[dict.LengthInBytes()];
            dict.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benDictionaryLengthInBytes()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            var dict = (BEncodedDictionary) BEncodedValue.Decode(data);

            Assert.Equal(data.Length, dict.LengthInBytes());
        }

        [Fact]
        public void benDictionaryStackedTest()
        {
            var benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";
            var data = Encoding.UTF8.GetBytes(benString);
            var dict = (BEncodedDictionary) BEncodedValue.Decode(data);
            var decoded = Encoding.UTF8.GetString(dict.Encode());
            Assert.Equal(benString, decoded);
        }

        [Fact]
        public void benListDecoding()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            using (Stream stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.Equal(result.ToString(), "l4:test5:tests6:testede");
                Assert.Equal(result is BEncodedList, true);
                var list = (BEncodedList) result;

                Assert.Equal(list.Count, 3);
                Assert.Equal(list[0] is BEncodedString, true);
                Assert.Equal(((BEncodedString) list[0]).Text, "test");
                Assert.Equal(((BEncodedString) list[1]).Text, "tests");
                Assert.Equal(((BEncodedString) list[2]).Text, "tested");
            }
        }

        [Fact]
        public void benListEncoding()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = new BEncodedList();
            list.Add(new BEncodedString("test"));
            list.Add(new BEncodedString("tests"));
            list.Add(new BEncodedString("tested"));

            Assert.True(Toolbox.ByteMatch(data, list.Encode()));
        }

        [Fact]
        public void benListEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = new BEncodedList();
            list.Add(new BEncodedString("test"));
            list.Add(new BEncodedString("tests"));
            list.Add(new BEncodedString("tested"));
            var result = new byte[list.LengthInBytes()];
            list.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benListLengthInBytes()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = (BEncodedList) BEncodedValue.Decode(data);

            Assert.Equal(data.Length, list.LengthInBytes());
        }

        [Fact]
        public void benListStackedTest()
        {
            var benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";
            var data = Encoding.UTF8.GetBytes(benString);
            var list = (BEncodedList) BEncodedValue.Decode(data);
            var decoded = Encoding.UTF8.GetString(list.Encode());
            Assert.Equal(benString, decoded);
        }

        [Fact]
        public void benNumberDecoding()
        {
            var data = Encoding.UTF8.GetBytes("i12412e");
            using (Stream stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.Equal(result is BEncodedNumber, true);
                Assert.Equal(result.ToString(), "12412");
                Assert.Equal(((BEncodedNumber) result).Number, 12412);
            }
        }

        [Fact]
        public void benNumberEncoding()
        {
            var data = Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding2()
        {
            var data = Encoding.UTF8.GetBytes("i0e");
            BEncodedNumber number = 0;
            Assert.Equal(3, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding3()
        {
            var data = Encoding.UTF8.GetBytes("i1230e");
            BEncodedNumber number = 1230;
            Assert.Equal(6, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding4()
        {
            var data = Encoding.UTF8.GetBytes("i-1230e");
            BEncodedNumber number = -1230;
            Assert.Equal(7, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding5()
        {
            var data = Encoding.UTF8.GetBytes("i-123e");
            BEncodedNumber number = -123;
            Assert.Equal(6, number.LengthInBytes());
            Assert.True(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Fact]
        public void benNumberEncoding6()
        {
            BEncodedNumber a = -123;
            var b = BEncodedValue.Decode<BEncodedNumber>(a.Encode());
            Assert.Equal(a.Number, b.Number);
        }

        [Fact]
        public void benNumberEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            var result = new byte[number.LengthInBytes()];
            number.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benNumberLengthInBytes()
        {
            var number = 1635;
            BEncodedNumber num = number;
            Assert.Equal(number.ToString().Length + 2, num.LengthInBytes());
        }

        [Fact]
        public void benStringDecoding()
        {
            var data = Encoding.UTF8.GetBytes("21:this is a test string");
            using (var stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.Equal("this is a test string", result.ToString());
                Assert.Equal(result is BEncodedString, true);
                Assert.Equal(((BEncodedString) result).Text, "this is a test string");
            }
        }

        [Fact]
        public void benStringEncoding()
        {
            var data = Encoding.UTF8.GetBytes("22:this is my test string");

            var benString = new BEncodedString("this is my test string");
            Assert.True(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Fact]
        public void benStringEncoding2()
        {
            var data = Encoding.UTF8.GetBytes("0:");

            var benString = new BEncodedString("");
            Assert.True(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Fact]
        public void benStringEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("22:this is my test string");

            var benString = new BEncodedString("this is my test string");
            var result = new byte[benString.LengthInBytes()];
            benString.Encode(result, 0);
            Assert.True(Toolbox.ByteMatch(data, result));
        }

        [Fact]
        public void benStringLengthInBytes()
        {
            var text = "thisisateststring";

            BEncodedString str = text;
            var length = text.Length;
            length += text.Length.ToString().Length;
            length++;

            Assert.Equal(length, str.LengthInBytes());
        }

        [Fact]
        public void corruptBenDataDecode()
        {
            var testString = "corruption!";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString)));
        }


        [Fact]
        public void corruptBenDictionaryDecode()
        {
            var testString = "d3:3521:a3:aedddd";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString)));
        }

        [Fact]
        public void corruptBenListDecode()
        {
            var testString = "l3:3521:a3:ae";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString)));
        }

        [Fact]
        public void corruptBenNumberDecode()
        {
            var testString = "i35212";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString)));
        }

        [Fact]
        public void corruptBenStringDecode()
        {
            var testString = "50:i'm too short";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString)));
        }

        [Fact]
        public void corruptBenStringDecode2()
        {
            var s = "d8:completei2671e10:incompletei669e8:intervali1836e12min intervali918e5:peers0:e";
            Assert.Throws<BEncodingException>(() => BEncodedValue.Decode(Encoding.ASCII.GetBytes(s)));
        }

        [Fact]
        public void UTF8Test()
        {
            var s = "ã";
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
    }
}