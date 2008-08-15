using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tests;

namespace MonoTorrent.Common.Tests
{
    [TestFixture]
    public class TorrentCreatorTests
    {
        //static void Main(string[] args)
        //{
        //    TorrentCreatorTests t = new TorrentCreatorTests();
        //    t.Setup();
        //}
        private TorrentCreator creator;

        [SetUp]
        public void Setup()
        {
            TestRig rig = new TestRig("");
            creator = new TorrentCreator();
            creator.Announces.Add(new List<string>(new string[] { "http://tracker1.com/announce1", "http://tracker2.com/announce2" }));
        }

        [Test]
        public void AddAnnouncesTest()
        {
            creator.Announces.Add(new List<string>(new string[] { "http://tier1/tracker1/announce", "http://tier1/tracker2/announce" }));
            creator.Announces.Add(new List<string>(new string[] { "http://tier2/tracker2/announce", "http://tier1/tracker2/announce" }));
        }
    }
}
