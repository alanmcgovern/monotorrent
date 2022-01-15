using System;

using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class VersionInfoTests
    {
        [Test]
        public void ValidVersionNumber ()
        {
            GitInfoHelper.Initialize (new Version (1, 2, 3));
            Assert.AreEqual (GitInfoHelper.ClientIdentifier + "1203", GitInfoHelper.ClientVersion);
            Assert.AreEqual (GitInfoHelper.DhtClientVersion, GitInfoHelper.ClientVersion);
        }

        [Test]
        public void MajorTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => GitInfoHelper.Initialize (new Version (10, 2, 3)));
        }

        [Test]
        public void MinorTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => GitInfoHelper.Initialize (new Version (1, 10, 3)));
        }

        [Test]
        public void BuildMissing ()
        {
            Assert.Throws<ArgumentException> (() => GitInfoHelper.Initialize (new Version (1, 2)));
        }

        [Test]
        public void BuildTooLarge ()
        {
            Assert.Throws<ArgumentException> (() => GitInfoHelper.Initialize (new Version (1, 10, 100)));
        }
    }
}

