using System;
using System.Collections.Generic;
using MonoTorrent.Client;
using MonoTorrent.Common;
using Xunit;

namespace MonoTorrent.Tests.Client
{
    public class PriorityPickerTests : IDisposable
    {
        public PriorityPickerTests()
        {
            rig = TestRig.CreateMultiFile();
            id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            id.BitField.SetAll(true);

            id.BitField.SetAll(true);
            tester = new TestPicker();
            picker = new PriorityPicker(tester);
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            foreach (var file in rig.Torrent.Files)
                file.Priority = Priority.Normal;
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        //static void Main()
        //{
        //    PriorityPickerTests t = new PriorityPickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.PriorityMix();
        //}

        private readonly PeerId id;
        private readonly PriorityPicker picker;
        private readonly TestRig rig;
        private readonly TestPicker tester;

        [Fact]
        public void AllAllowed()
        {
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(1, tester.PickPieceBitfield.Count);
            Assert.True(tester.PickPieceBitfield[0].AllTrue);
        }

        [Fact]
        public void DoNotDownload()
        {
            rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            rig.Torrent.Files[1].Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(1, tester.PickPieceBitfield.Count);
            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.False(tester.PickPieceBitfield[0][i]);
                else
                    Assert.True(tester.PickPieceBitfield[0][i]);
            }
        }

        [Fact]
        public void HighPriority()
        {
            rig.Torrent.Files[0].Priority = Priority.High;
            rig.Torrent.Files[1].Priority = Priority.High;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(2, tester.PickPieceBitfield.Count);
            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i <= rig.Torrent.Files[1].EndPieceIndex)
                    Assert.True(tester.PickPieceBitfield[0][i]);
                else
                    Assert.False(tester.PickPieceBitfield[0][i]);
            }

            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.False(tester.PickPieceBitfield[1][i]);
                else
                    Assert.True(tester.PickPieceBitfield[1][i]);
            }
        }

        [Fact]
        public void IsInteresting()
        {
            foreach (var file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            rig.Torrent.Files[1].Priority = Priority.High;
            id.BitField.SetAll(false).Set(0, true);
            Assert.True(picker.IsInteresting(id.BitField));
        }

        [Fact]
        public void MultiFileAllNoDownload()
        {
            foreach (var file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void MultiFileNoneAvailable()
        {
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            id.BitField.SetAll(false);

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void MultiFileOneAvailable()
        {
            foreach (var file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            rig.Torrent.Files[0].Priority = Priority.High;
            id.BitField.SetAll(false);
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void PriorityMix()
        {
            BitField bf;
            TorrentFile file;

            rig.Torrent.Files[0].Priority = Priority.Immediate;
            rig.Torrent.Files[1].Priority = Priority.Low;
            rig.Torrent.Files[2].Priority = Priority.DoNotDownload;
            rig.Torrent.Files[3].Priority = Priority.High;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);

            Assert.Equal(3, tester.PickPieceBitfield.Count);

            bf = tester.PickPieceBitfield[0];
            file = rig.Torrent.Files[0];
            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
            }

            bf = tester.PickPieceBitfield[1];
            file = rig.Torrent.Files[3];
            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
            }

            bf = tester.PickPieceBitfield[2];
            file = rig.Torrent.Files[1];
            for (var i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
            }
        }

        [Fact]
        public void SingleFileDoNotDownload()
        {
            picker.Initialise(rig.Manager.Bitfield, new[] {rig.Torrent.Files[0]}, new List<Piece>());
            rig.Torrent.Files[0].Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void SingleFileNoneAvailable()
        {
            picker.Initialise(rig.Manager.Bitfield, new[] {rig.Torrent.Files[0]}, new List<Piece>());
            id.BitField.SetAll(false);

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }
    }
}