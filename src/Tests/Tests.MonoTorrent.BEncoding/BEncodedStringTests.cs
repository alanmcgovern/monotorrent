using System;
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;

namespace MonoTorrent.BEncoding
{
    [TestFixture]
    public class BEncodedStringTests
    {
        [Test]
        public void NullCtorTests ()
        {
            Assert.Throws<ArgumentNullException> (() => new BEncodedString ((byte[]) null));
            Assert.Throws<ArgumentNullException> (() => new BEncodedString ((char[]) null));
            Assert.Throws<ArgumentNullException> (() => new BEncodedString ((string) null));
        }

        [Test]
        public void EmptyStringTests ()
        {
            var variants = new[] {
                new BEncodedString (Array.Empty<byte> ()),
                new BEncodedString (Array.Empty<char> ()),
                new BEncodedString (""),
                BEncodedString.FromMemory (null),
                BEncodedString.FromMemory (Memory<byte>.Empty),
            };

            foreach (var variant in variants) {
                Assert.IsTrue (BEncodedString.IsNullOrEmpty (variant));
                Assert.AreEqual (2, variant.LengthInBytes ());
                Assert.AreEqual ("", variant.Text);
                Assert.AreEqual (0, variant.Span.Length);
            }
        }

        [Test]
        public void benStringDecoding ()
        {
            foreach (var str in new[] { "", "a", "this is a test string" }) {
                var data = Encoding.UTF8.GetBytes ($"{str.Length}:{str}");
                foreach (var result in BEncodedValue.DecodingVariants<BEncodedString> (data)) {
                    Assert.AreEqual (result.Text, str);
                    Assert.AreEqual (str, result.ToString ());
                }
            }
        }

        [Test]
        public void benStringEncoding ()
        {
            Span<byte> data = Encoding.UTF8.GetBytes ("22:this is my test string");

            BEncodedString benString = new BEncodedString ("this is my test string");
            Assert.IsTrue (data.SequenceEqual (benString.Encode ()));
        }

        [Test]
        public void benStringEncoding2 ()
        {
            Span<byte> data = Encoding.UTF8.GetBytes ("0:");

            BEncodedString benString = new BEncodedString ("");
            Assert.IsTrue (data.SequenceEqual (benString.Encode ()));
        }

        [Test]
        public void benStringEncodingBuffered ()
        {
            Span<byte> data = Encoding.UTF8.GetBytes ("22:this is my test string");

            BEncodedString benString = new BEncodedString ("this is my test string");
            byte[] result = new byte[benString.LengthInBytes ()];
            benString.Encode (result.AsSpan ());
            Assert.IsTrue (data.SequenceEqual (result));
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
            var data = Encoding.UTF8.GetBytes ("50:i'm too short");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }

        [Test]
        public void corruptBenStringDecode2 ()
        {
            var data = Encoding.UTF8.GetBytes ("d8:completei2671e10:incompletei669e8:intervali1836e12min intervali918e5:peers0:e");
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (data));
            Assert.Throws<BEncodingException> (() => BEncodedValue.Decode (new MemoryStream (data)));
        }
    }
}
