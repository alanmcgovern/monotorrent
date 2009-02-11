//
// EndGameSwitcher.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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


using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class EndGameSwitcher : PiecePicker
    {
        const int Threshold = 20;

        BitField bitfield;
        int blocksPerPiece;
        bool inEndgame;
        EndGamePicker endgame;
        BitField endgameSelector;
        TorrentFile[] files;
        StandardPicker standard;

        PiecePicker ActivePicker
        {
            get { return inEndgame ? (PiecePicker)endgame : standard; }
        }

        public EndGameSwitcher(StandardPicker standard, EndGamePicker endgame, int blocksPerPiece)
            : base(null)
        {
            this.standard = standard;
            this.endgame = endgame;
            this.blocksPerPiece = blocksPerPiece;
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            ActivePicker.CancelRequest(peer, piece, startOffset, length);
        }

        public override void CancelRequests(PeerId peer)
        {
            ActivePicker.CancelRequests(peer);
        }

        public override void CancelTimedOutRequests()
        {
            ActivePicker.CancelTimedOutRequests();
        }

        public override int CurrentRequestCount()
        {
            return ActivePicker.CurrentRequestCount();
        }

        public override List<Piece> ExportActiveRequests()
        {
            return ActivePicker.ExportActiveRequests();
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            this.bitfield = bitfield;
            this.endgameSelector = new BitField(bitfield.Length);
            this.files = files;
            inEndgame = false;
            TryEnableEndgame();
            ActivePicker.Initialise(bitfield, files, requests);
        }

        public override bool IsInteresting(MonoTorrent.Common.BitField bitfield)
        {
            return ActivePicker.IsInteresting(bitfield);
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            TryEnableEndgame();
            return ActivePicker.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
        }

        private void TryEnableEndgame()
        {
            if (inEndgame)
                return;

            endgameSelector.SetAll(true);
            for (int i = 0; i < files.Length; i++)
                if (files[i].Priority == Priority.DoNotDownload)
                    endgameSelector.Xor(files[i].GetSelector(bitfield.Length));

            endgameSelector.NAnd(bitfield);
            inEndgame = Math.Max(blocksPerPiece, (endgameSelector.TrueCount * blocksPerPiece)) < Threshold;
            if (inEndgame)
                endgame.Initialise(bitfield, files, standard.ExportActiveRequests());
        }

        public override void Reset()
        {
            inEndgame = false;
            standard.Reset();
            endgame.Reset();
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            return ActivePicker.ValidatePiece(peer, pieceIndex, startOffset, length, out piece);
        }
    }
}
