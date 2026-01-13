using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent;

using NUnit.Framework;

namespace Tests.MonoTorrent
{
    [TestFixture]
    class SpanStringListTests
    {
        [Test]
        [TestCaseSource(nameof(EqualsSource))]
        public void Equals (object a, object b)
        {
            Assert.That (a, Is.EqualTo (b));
            if (a is string a1 && b is SpanStringList b1) {
                Assert.True (a1 == b1);
                Assert.False (a1 != b1);
            }
            if (a is SpanStringList a2 && b is string b2) {
                Assert.True (a2 == b2);
                Assert.False (a2 != b2);
            }

            if (a is SpanStringList a3 && b is SpanStringList b3) {
                Assert.That (a3.GetHashCode (), Is.EqualTo (b3.GetHashCode ()));
                Assert.True (a3 == b3);
                Assert.False (a3 != b3);
            }
        }

        public static IEnumerable EqualsSource ()
        {
            yield return new object[] { new SpanStringList ("123456"), "123456" };
            yield return new object[] { new SpanStringList ("123456"), new SpanStringList("123").Append ("456") };
            yield return new object[] { "123456", new SpanStringList("123").Append ("456") };
            yield return new object[] { new SpanStringList ("1234"), new SpanStringList("1").Append ("2").Append ("3").Append ("4") };
            yield return new object[] { "1234", new SpanStringList("1").Append ("2").Append ("3").Append ("4") };
            yield return new object[] { new SpanStringList ("123").Append ("456").Append ("789").Append ("0"), new SpanStringList("12").Append ("34").Append ("56").Append ("78").Append ("90") };
        }

        [Test]
        [TestCaseSource(nameof(NotEqualsSource))]
        public void NotEquals (object a, object b)
        {
            Assert.That (a, Is.Not.EqualTo (b));
            if (a is string a1 && b is SpanStringList b1) {
                Assert.False (a1 == b1);
                Assert.True (a1 != b1);
            }
            if (a is SpanStringList a2 && b is string b2) {
                Assert.False (a2 == b2);
                Assert.True (a2 != b2);
            }

            if (a is SpanStringList a3 && b is SpanStringList b3) {
                Assert.False (a3 == b3);
                Assert.True (a3 != b3);
            }
        }

        public static IEnumerable NotEqualsSource ()
        {
            yield return new object[] { new SpanStringList ("12345"), "12456" };
            yield return new object[] { new SpanStringList ("123456"), new SpanStringList("123").Append ("46") };
            yield return new object[] { "z", new SpanStringList("123").Append ("456") };
            yield return new object[] { new SpanStringList ("1234"), new SpanStringList("1").Append ("2").Append ("4") };
            yield return new object[] { "123", new SpanStringList("1").Append ("2").Append ("3").Append ("4") };
            yield return new object[] { new SpanStringList ("123").Append ("456").Append ("79").Append ("0"), new SpanStringList("12").Append ("34").Append ("6").Append ("78").Append ("90") };
        }
    }
}
