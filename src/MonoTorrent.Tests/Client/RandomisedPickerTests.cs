using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MonoTorrent.Client
{
    
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

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            rig = TestRig.CreateMultiFile();
            id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            for (int i = 0; i < id.BitField.Length; i += 2)
                id.BitField[i] = true;
        }

        [OneTimeTearDown]
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

        [Fact]
        public void EnsureRandomlyPicked()
        {
            tester.ReturnNoPiece = false;
            while (picker.PickPiece(id, new List<PeerId>(), 1) != null) { }
            Assert.Equal(rig.Torrent.Pieces.Count, tester.PickedPieces.Count);
            List<int> pieces = new List<int>(tester.PickedPieces);
            pieces.Sort();
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != tester.PickedPieces[i])
                    return;
            Assert.Fail("The piece were picked in order");
        }
    }
}
