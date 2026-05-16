using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class PathPartComparerTests
    {
        [Test]
        public void Basic ()
        {
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("a", "a"));
            Assert.Less (PathPartComparer.Instance.Compare ("a", "b"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("c", "b"), 0);

            Assert.Less (PathPartComparer.Instance.Compare ("a", "aa"), 0);
            Assert.Less (PathPartComparer.Instance.Compare ("a", "aa"), 0);
            Assert.Less (PathPartComparer.Instance.Compare ("a", "ab"), 0);

            Assert.Greater (PathPartComparer.Instance.Compare ("aa", "a"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("aa", "a"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("ab", "a"), 0);

            Assert.Less (PathPartComparer.Instance.Compare ("a", "a/a"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("a a", "a/a"), 0);
            Assert.Less (PathPartComparer.Instance.Compare ("a/a", "a a"), 0);
            Assert.Less (PathPartComparer.Instance.Compare ("a/a", "a a/a"), 0);
        }

        [Test]
        public void Separators ()
        {
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("a/b", "a/b"));
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("a/b", "a\\b"));
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("a\\b", "a/b"));
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("a\\b", "a\\b"));
        }

        [Test]
        public void ComponentBoundaries ()
        {
            Assert.Less (PathPartComparer.Instance.Compare ("a/bb/c", "a/bb/cc"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("a/bb/cc", "a/bb/c"), 0);
            Assert.Less (PathPartComparer.Instance.Compare ("a/b/b", "a/b/c"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("a/b/c", "a/b/b"), 0);
        }

        [Test]
        public void EmptyPaths ()
        {
            Assert.AreEqual (0, PathPartComparer.Instance.Compare ("", ""));
            Assert.Less (PathPartComparer.Instance.Compare ("", "a"), 0);
            Assert.Greater (PathPartComparer.Instance.Compare ("a", ""), 0);
        }

        [Test]
        public void Antisymmetry ()
        {
            string[] values = {
                "",
                "a",
                "a/b",
                "a//b",
                "b",
                "a/c"
            };

            foreach (var a in values) {
                foreach (var b in values) {
                    int ab = Math.Sign (PathPartComparer.Instance.Compare (a, b));
                    int ba = Math.Sign (PathPartComparer.Instance.Compare (b, a));

                    Assert.AreEqual (ab, -ba);
                }
            }
        }

        [Test]
        public void Equivalence ()
        {
            string[] values = {
                "",
                "a",
                "a/b",
                "a//b",
                "b",
                "a/c"
            };

            foreach (var a in values)
                Assert.AreEqual (0, PathPartComparer.Instance.Compare (a, a));
        }
    }
}
