using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class RandomisedPickerTests
    {
        //static void Main()
        //{
        //    RandomisedPickerTests t = new RandomisedPickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.Pick();
        //}

        PeerId id;
        RandomisedPicker picker;
        TestRig rig;
        TestPicker tester;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            rig = new TestRig("");
            id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            for (int i = 0; i < id.BitField.Length; i += 2)
                id.BitField[i] = true;
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            tester = new TestPicker();
            picker = new RandomisedPicker(tester);
        }

        [Test]
        public void Pick()
        {
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 10, 40, 1);
            Assert.AreEqual(2, tester.PickPieceEndIndex.Count, "#1");
            Assert.AreEqual(2, tester.PickPieceStartIndex.Count, "#2");
            Assert.AreEqual(2, tester.PickPieceCount.Count, "#3");
            if (tester.PickPieceStartIndex[0] != 10)
            {
                Assert.AreEqual(tester.PickPieceStartIndex[0], tester.PickPieceEndIndex[1], "#4");
                Assert.AreEqual(10, tester.PickPieceStartIndex[1], "#5");
            }
            else
            {
                Assert.AreEqual(tester.PickPieceEndIndex[0], tester.PickPieceStartIndex[1], "#6");
                Assert.AreEqual(40, tester.PickPieceEndIndex[1], "#7");
            }
        }
    }
}
