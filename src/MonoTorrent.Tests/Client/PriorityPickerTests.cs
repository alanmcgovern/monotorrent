//
// PriorityPickerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using MonoTorrent.Common;
using System;
using System.Collections.Generic;
using Xunit;

namespace MonoTorrent.Client
{

    public class PriorityPickerTests : IDisposable
    {
        //static void Main()
        //{
        //    PriorityPickerTests t = new PriorityPickerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.PriorityMix();
        //}

        PeerId id;
        PriorityPicker picker;
        TestRig rig;
        TestPicker tester;

        public PriorityPickerTests()
        {
            rig = TestRig.CreateMultiFile();
            id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), rig.Manager);
            id.BitField.SetAll(true);

            id.BitField.SetAll(true);
            tester = new TestPicker();
            picker = new PriorityPicker(tester);
            picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            foreach (TorrentFile file in rig.Torrent.Files)
                file.Priority = Priority.Normal;
        }

        public void Dispose()
        {
            rig.Dispose();
        }

        [Fact]
        public void AllAllowed()
        {
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(1, tester.PickPieceBitfield.Count);
            Assert.True(tester.PickPieceBitfield[0].AllTrue);
        }

        [Fact]
        public void HighPriority()
        {
            rig.Torrent.Files[0].Priority = Priority.High;
            rig.Torrent.Files[1].Priority = Priority.High;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(2, tester.PickPieceBitfield.Count);
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i <= rig.Torrent.Files[1].EndPieceIndex)
                    Assert.True(tester.PickPieceBitfield[0][i]);
                else
                    Assert.False(tester.PickPieceBitfield[0][i]);
            }

            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.False(tester.PickPieceBitfield[1][i]);
                else
                    Assert.True(tester.PickPieceBitfield[1][i]);
            }
        }

        [Fact]
        public void DoNotDownload()
        {
            rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            rig.Torrent.Files[1].Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(1, tester.PickPieceBitfield.Count);
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i < rig.Torrent.Files[1].EndPieceIndex)
                    Assert.False(tester.PickPieceBitfield[0][i]);
                else
                    Assert.True(tester.PickPieceBitfield[0][i]);
            }
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
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
            }

            bf = tester.PickPieceBitfield[1];
            file = rig.Torrent.Files[3];
            for (int i = 0; i < rig.Pieces; i++)
            {
                if (i >= file.StartPieceIndex && i <= file.EndPieceIndex)
                    Assert.True(bf[i]);
                else
                    Assert.False(bf[i]);
            }

            bf = tester.PickPieceBitfield[2];
            file = rig.Torrent.Files[1];
            for (int i = 0; i < rig.Pieces; i++)
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
            this.picker.Initialise(rig.Manager.Bitfield, new TorrentFile[] { rig.Torrent.Files[0] }, new List<Piece>());
            rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void SingleFileNoneAvailable()
        {
            this.picker.Initialise(rig.Manager.Bitfield, new TorrentFile[] { rig.Torrent.Files[0] }, new List<Piece>());
            id.BitField.SetAll(false);

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void MultiFileNoneAvailable()
        {
            this.picker.Initialise(rig.Manager.Bitfield, rig.Torrent.Files, new List<Piece>());
            id.BitField.SetAll(false);

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void MultiFileAllNoDownload()
        {
            foreach (TorrentFile file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;

            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void MultiFileOneAvailable()
        {
            foreach (TorrentFile file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            rig.Torrent.Files[0].Priority = Priority.High;
            id.BitField.SetAll(false);   
            picker.PickPiece(id, id.BitField, new List<PeerId>(), 1, 0, rig.Pieces);
            Assert.Equal(0, tester.PickPieceBitfield.Count);
        }

        [Fact]
        public void IsInteresting()
        {
            foreach (TorrentFile file in rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            rig.Torrent.Files[1].Priority = Priority.High;
            id.BitField.SetAll(false).Set(0, true);
            Assert.True(picker.IsInteresting(id.BitField));
        }
    }
}
