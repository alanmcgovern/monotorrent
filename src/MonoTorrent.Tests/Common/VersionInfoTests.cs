using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class VersionInfoTests
    {
        [Test]
        public void ValidVersionNumber ()
        {
            VersionInfo.Initialize (new Version (1, 2, 3));
            Assert.AreEqual (VersionInfo.ClientIdentifier + "1203", VersionInfo.ClientVersion);
            Assert.AreEqual (VersionInfo.DhtClientVersion, VersionInfo.ClientVersion);
        }

        [Test]
        public void MajorTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => VersionInfo.Initialize (new Version (10, 2, 3)));
        }

        [Test]
        public void MinorTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => VersionInfo.Initialize (new Version (1, 10, 3)));
        }

        [Test]
        public void BuildMissing ()
        {
            Assert.Throws<ArgumentException> (() => VersionInfo.Initialize (new Version (1, 2)));
        }

        [Test]
        public void BuildTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => VersionInfo.Initialize (new Version (1, 10, 100)));
        }
    }
}

