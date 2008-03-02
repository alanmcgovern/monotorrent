using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tests
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        private TorrentCreator creator;

        [SetUp]
        public void Setup()
        {
            creator = new TorrentCreator();
        }

        [Test]
        public void AddAnnouncesTest()
        {
            creator.Announces.Add(new List<string>(new string[] { "http://tier1/tracker1/announce", "http://tier1/tracker2/announce" }));
            creator.Announces.Add(new List<string>(new string[] { "http://tier2/tracker2/announce", "http://tier1/tracker2/announce" }));
        }
    }
}
