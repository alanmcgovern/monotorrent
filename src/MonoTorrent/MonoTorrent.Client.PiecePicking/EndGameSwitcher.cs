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
using System.Linq;

namespace MonoTorrent.Client.PiecePicking
{
    public class EndGameSwitcher : PiecePicker
    {
        /// <summary>
        /// Allow entering endgame mode if there are fewer than 256 blocks outstanding.
        /// </summary>
        const int Threshold = 256;

        BitField bitfield;
        bool inEndgame;
        PiecePicker endgame;
        BitField endgameSelector;
        ITorrentData torrentData;
        PiecePicker standard;
        TorrentManager torrentManager;

        public PiecePicker ActivePicker => inEndgame ? endgame : standard;

        public EndGameSwitcher (PiecePicker standard, EndGamePicker endgame, TorrentManager torrentManager)
            : base (null)
        {
            this.standard = standard;
            this.endgame = endgame;
            this.torrentManager = torrentManager;
        }

        [Obsolete("Use the constructor overload which does not specify 'blocksPerPiece'. The 'blocksPerPiece' value is calculated from the ITorrentData")]
        public EndGameSwitcher(StandardPicker standard, EndGamePicker endgame, int blocksPerPiece, TorrentManager torrentManager)
            : this(standard, endgame, torrentManager)
        {
        }

        public override void CancelRequest(IPieceRequester peer, int piece, int startOffset, int length)
        {
            ActivePicker.CancelRequest(peer, piece, startOffset, length);
        }

        public override void CancelRequests(IPieceRequester peer)
        {
            ActivePicker.CancelRequests(peer);
        }

        public override void CancelTimedOutRequests()
        {
            ActivePicker.CancelTimedOutRequests();
        }

        public override PieceRequest ContinueExistingRequest(IPieceRequester peer)
        {
            return ActivePicker.ContinueExistingRequest(peer);
        }

        public override int CurrentReceivedCount()
        {
            return ActivePicker.CurrentReceivedCount();
        }

        public override int CurrentRequestCount()
        {
            return ActivePicker.CurrentRequestCount();
        }

        public override List<Piece> ExportActiveRequests()
        {
            return ActivePicker.ExportActiveRequests();
        }

        public override void Initialise(BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {
            this.bitfield = bitfield;
            this.endgameSelector = new BitField(bitfield.Length);
            this.torrentData = torrentData;
            inEndgame = false;

            // Always initialize both pickers, but we should only give the active requests to the Standard picker.
            // We should never *default* to endgame mode, we should always start in regular mode and enter endgame
            // mode after we fail to pick a piece.
            standard.Initialise (bitfield, torrentData, requests);
            endgame.Initialise  (bitfield, torrentData, Enumerable.Empty<Piece> ());
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return ActivePicker.IsInteresting(bitfield);
        }

        public override IList<PieceRequest> PickPiece(IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            var bundle = ActivePicker.PickPiece(peer, available, otherPeers, count, startIndex, endIndex);
            if (bundle == null && TryEnableEndgame())
                return ActivePicker.PickPiece(peer, available, otherPeers, count, startIndex, endIndex);
            return bundle;
        }

        private bool TryEnableEndgame()
        {
            if (inEndgame)
                return false;

            // We need to activate endgame mode when there are less than 20 requestable blocks
            // available. We OR the bitfields of all the files which are downloadable and then
            // NAND it with the torrents bitfield to get a list of pieces which remain to be downloaded.

            // Essentially we get a list of all the pieces we're allowed download, then get a list of
            // the pieces which we still need to get and AND them together.

            // Create the bitfield of pieces which are downloadable
            endgameSelector.SetAll(false);
            for (int i = 0; i < torrentData.Files.Length; i++)
                if (torrentData.Files[i].Priority != Priority.DoNotDownload)
                    endgameSelector.Or(torrentData.Files[i].GetSelector(bitfield.Length));

            // NAND it with the pieces we already have (i.e. AND it with the pieces we still need to receive)
            endgameSelector.NAnd(bitfield);

            // If the total number of blocks remaining is less than Threshold, activate Endgame mode.
            int count = standard.CurrentReceivedCount ();
            int blocksPerPiece = torrentData.PieceLength / Piece.BlockSize;
            inEndgame = Math.Max(blocksPerPiece, (endgameSelector.TrueCount * blocksPerPiece)) - count <= Threshold;
            if (inEndgame)
            {
                endgame.Initialise(bitfield, torrentData, standard.ExportActiveRequests());
                standard.Reset ();
                // Set torrent's IsInEndGame flag
                if (torrentManager != null)
                    torrentManager.isInEndGame = true;
            }
            return inEndgame;
        }

        public override void Reset()
        {
            inEndgame = false;
            if (torrentManager != null)
                torrentManager.isInEndGame = false;
            standard.Reset();
            endgame.Reset();
        }

        public override bool ValidatePiece(IPieceRequester peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            return ActivePicker.ValidatePiece(peer, pieceIndex, startOffset, length, out piece);
        }
    }
}
