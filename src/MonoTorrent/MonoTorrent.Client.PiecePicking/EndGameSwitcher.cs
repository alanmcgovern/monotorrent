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
    public class EndGameSwitcher : IPiecePicker
    {
        /// <summary>
        /// Allow entering endgame mode if there are fewer than 256 blocks outstanding.
        /// </summary>
        const int Threshold = 256;

        BitField bitfield;
        readonly IPiecePicker endgame;
        BitField endgameSelector;
        ITorrentData torrentData;
        readonly IPiecePicker standard;

        public IPiecePicker ActivePicker => InEndgame ? endgame : standard;

        public bool InEndgame { get; private set; }

        public EndGameSwitcher (IPiecePicker standard, EndGamePicker endgame)
        {
            this.standard = standard;
            this.endgame = endgame;
        }

        public int AbortRequests (IPieceRequester peer)
            => ActivePicker.AbortRequests (peer);

        public void RequestRejected (IPieceRequester peer, PieceRequest rejectedRequest)
            => ActivePicker.RequestRejected (peer, rejectedRequest);

        public IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex)
            => ActivePicker.CancelRequests (peer, startIndex, endIndex);

        public PieceRequest? ContinueAnyExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
        {
            var bundle = ActivePicker.ContinueAnyExistingRequest (peer, startIndex, endIndex);
            if (bundle == null && TryEnableEndgame ())
                return ActivePicker.ContinueAnyExistingRequest (peer, startIndex, endIndex);
            return bundle;
        }

        public PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
            => ActivePicker.ContinueExistingRequest (peer, startIndex, endIndex);

        public int CurrentReceivedCount ()
            => ActivePicker.CurrentReceivedCount ();

        public int CurrentRequestCount ()
            => ActivePicker.CurrentRequestCount ();

        public IList<ActivePieceRequest> ExportActiveRequests ()
            => ActivePicker.ExportActiveRequests ();

        public void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
        {
            this.bitfield = bitfield;
            this.torrentData = torrentData;

            endgameSelector = new BitField (bitfield.Length);
            InEndgame = false;

            // Always initialize both pickers, but we should only give the active requests to the Standard picker.
            // We should never *default* to endgame mode, we should always start in regular mode and enter endgame
            // mode after we fail to pick a piece.
            standard.Initialise (bitfield, torrentData, requests);
            endgame.Initialise (bitfield, torrentData, Enumerable.Empty<ActivePieceRequest> ());
        }

        public bool IsInteresting (IPieceRequester peer, BitField bitfield)
            => ActivePicker.IsInteresting (peer, bitfield);

        public IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
            => ActivePicker.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

        public void Tick ()
            => ActivePicker.Tick ();

        public bool ValidatePiece (IPieceRequester peer, PieceRequest request, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
            => ActivePicker.ValidatePiece (peer, request, out pieceComplete, out peersInvolved);

        bool TryEnableEndgame ()
        {
            if (InEndgame)
                return false;

            // We need to activate endgame mode when there are less than 20 requestable blocks
            // available. We OR the bitfields of all the files which are downloadable and then
            // NAND it with the torrents bitfield to get a list of pieces which remain to be downloaded.

            // Essentially we get a list of all the pieces we're allowed download, then get a list of
            // the pieces which we still need to get and AND them together.

            // Create the bitfield of pieces which are downloadable
            endgameSelector.SetAll (false);
            for (int i = 0; i < torrentData.Files.Count; i++)
                if (torrentData.Files[i].Priority != Priority.DoNotDownload)
                    endgameSelector.SetTrue (torrentData.Files[i].GetSelector ());

            // NAND it with the pieces we already have (i.e. AND it with the pieces we still need to receive)
            endgameSelector.NAnd (bitfield);

            // If the total number of blocks remaining is less than Threshold, activate Endgame mode.
            int count = standard.CurrentReceivedCount ();
            int blocksPerPiece = torrentData.PieceLength / Piece.BlockSize;
            InEndgame = Math.Max (blocksPerPiece, (endgameSelector.TrueCount * blocksPerPiece)) - count <= Threshold;
            if (InEndgame) {
                endgame.Initialise (bitfield, torrentData, standard.ExportActiveRequests ());
                standard.Initialise (bitfield, torrentData, Array.Empty<ActivePieceRequest> ());
            }
            return InEndgame;
        }
    }
}
