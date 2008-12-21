using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class RarestFirstPickerTests
    {
        static void Main()
        {
            RarestFirstPickerTests t = new RarestFirstPickerTests();
            t.FixtureSetup();
            t.Setup();
            t.RarestPieceTest();
        }
        TestRig rig;
        List<PeerId> peers;
        RarestFirstPicker rarest;
        TestPicker tester;


        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = new TestRig("");
        }

        [SetUp]
        public void Setup()
        {
            tester = new TestPicker();
            rarest = new RarestFirstPicker(tester);
            rarest.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            peers = new List<PeerId>();
            for (int i = 0; i < 5; i++)
                peers.Add(new PeerId(new Peer(new string((char)(i + 'a'), 20), new Uri("tcp://aaa")), rig.Manager));

            for (int i = 0; i < rig.Manager.Bitfield.Length; i++)
            {
                for (int j = 0; j < peers.Count; j++)
                    peers[j].BitField[i] = i % (j + 1) == 0;
            }
            peers[0].BitField.SetAll(true);
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [Test]
        public void RarestPieceTest()
        {
            rarest.PickPiece(peers[0], peers);
            Assert.AreEqual(5, tester.PickPieceBitfield.Count, "#1");
            BitField bf = tester.PickPieceBitfield[0];
            int[] trueIndices = new int[] { 1, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
            for (int i = 0; i < bf.Length; i++)
                if (Array.IndexOf<int>(trueIndices, i) > -1)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);

            bf = tester.PickPieceBitfield[1];
            trueIndices = new int[] { 1, 5, 7, 11, 13, 17, 19, 23, 25, 29, 31, 35, 37 };
            for (int i = 0; i < bf.Length; i++)
                if (Array.IndexOf<int>(trueIndices, i) > -1)
                    Assert.IsTrue(bf[i]);
                else
                    Assert.IsFalse(bf[i]);
        }
    }
}
