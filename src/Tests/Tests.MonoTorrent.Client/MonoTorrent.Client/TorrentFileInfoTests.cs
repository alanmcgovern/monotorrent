using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class TorrentFileInfoTests
    {
        static string[] ValidPaths => new[] {
            "Foo.cs",
            $"dir1{Path.DirectorySeparatorChar}Foo.cs",
            $"dir1{Path.DirectorySeparatorChar}dir2{Path.DirectorySeparatorChar}Foo.cs"
        };
        static string[] InvalidFilenames => new[] {
            $"{Path.GetInvalidFileNameChars ()[0]}Foo.cs",
            $"Fo{Path.GetInvalidFileNameChars ()[1]}o.cs",
            $"dir1{Path.DirectorySeparatorChar}dir2{Path.DirectorySeparatorChar}Fo{Path.GetInvalidFileNameChars ()[2]}o.cs"
        };

        static string[] InvalidPaths => new[] {
            $"dir1{Path.GetInvalidPathChars()[0]}asd{Path.DirectorySeparatorChar}Foo.cs",
            $"dir1{Path.GetInvalidPathChars()[1]}{Path.DirectorySeparatorChar}dir{Path.GetInvalidPathChars()[2]}2{Path.DirectorySeparatorChar}Foo.cs",
        };

        static string[] InvalidPathAndFilenames => new[] {
            $"dir{Path.GetInvalidPathChars()[0]}1{Path.DirectorySeparatorChar}dir2{Path.DirectorySeparatorChar}Fo{Path.GetInvalidFileNameChars()[0]}o.cs"
        };

        [Test]
        public void PathIsValid ([ValueSource(nameof(ValidPaths))] string path)
        {
            Assert.AreEqual (path, TorrentFileInfo.PathAndFileNameEscape (path));
            Assert.DoesNotThrow (() => Path.Combine (path, "test"));
        }

        [Test]
        public void PathContainsInvalidChar ([ValueSource(nameof(InvalidPaths))] string path)
        {
            var escaped = TorrentFileInfo.PathAndFileNameEscape (path);
            Assert.AreNotEqual (path, escaped);
            Assert.IsTrue (Path.GetInvalidFileNameChars ().All (t => !Path.GetFileName (escaped).Contains (t)));
            Assert.IsTrue (Path.GetInvalidPathChars ().All (t => !Path.GetDirectoryName (escaped).Contains (t)));
        }

        [Test]
        public void PathAndFilenameContainInvalidChars ([ValueSource (nameof (InvalidPathAndFilenames))] string path)
        {
            var escaped = TorrentFileInfo.PathAndFileNameEscape (path);
            Assert.AreNotEqual (path, escaped);
            Assert.IsTrue (Path.GetInvalidFileNameChars ().All (t => !Path.GetFileName (escaped).Contains (t)));
            Assert.IsTrue (Path.GetInvalidPathChars ().All (t => !Path.GetDirectoryName (escaped).Contains (t)));
        }

        [Test]
        public void FilenameContainsInvalidChar ([ValueSource (nameof (InvalidFilenames))] string path)
        {
            var escaped = TorrentFileInfo.PathAndFileNameEscape (path);
            Assert.AreNotEqual (path, escaped);
            Assert.IsTrue (Path.GetInvalidFileNameChars ().All (t => !Path.GetFileName (escaped).Contains (t)));
            Assert.IsTrue (Path.GetInvalidPathChars ().All (t => !Path.GetDirectoryName (escaped).Contains (t)));
        }
    }
}
