using System;
using System.Collections.Generic;
using Xunit;

namespace MonoTorrent.Client
{
    public class RarestFirstPickerTests : IDisposable
    {
        public RarestFirstPickerTests()
        {
            rig = TestRig.CreateMultiFile();

            tester = new TestPicker();
            rarest = new RarestFirstPicker(tester);
            rarest.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            peers = new List<PeerId>();
            for (var i = 0; i < 5; i++)
                peers.Add(new PeerId(new Peer(new string((char) (i + 'a'), 20), new Uri("tcp://aaa")), rig.Manager));

            for (var i = 0; i < rig.Manager.Bitfield.Length; i++)
            {
                for (var j = 0; j < peers.Count; j++)
                    peers[j].BitField[i] = i%(j + 1) == 0;
            }
            peers[0].BitField.SetAll(true);
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        //static void Main()
        //{
        //    RarestFirstPickerTests t = new RarestFirstPickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.RarestPieceTest();
        //}
        private readonly TestRig rig;
        private readonly List<PeerId> peers;
        private readonly RarestFirstPicker rarest;
        private readonly TestPicker tester;

        [Fact]
        public void RarestPieceTest()
        {
            rarest.PickPiece(peers[0], peers);
            Assert.Equal(5, tester.PickPieceBitfield.Count);
            var bf = tester.PickPieceBitfield[0];
            var trueIndices = new[] {1, 7, 11, 13, 17, 19, 23, 29, 31, 37};
            for (var i = 0; i < bf.Length; i++)
                if (Array.IndexOf(trueIndices, i) > -1)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);

            bf = tester.PickPieceBitfield[1];
            trueIndices = new[] {1, 5, 7, 11, 13, 17, 19, 23, 25, 29, 31, 35, 37};
            for (var i = 0; i < bf.Length; i++)
                if (Array.IndexOf(trueIndices, i) > -1)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
        }
    }
}