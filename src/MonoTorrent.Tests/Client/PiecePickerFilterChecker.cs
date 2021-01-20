﻿//
// PiecePickerFilterChecker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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


using System.Collections.Generic;

namespace MonoTorrent.Client.PiecePicking
{
    class PiecePickerFilterChecker : PiecePickerFilter
    {
        public List<(BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)> Initialised;
        public List<(IPieceRequester peer, BitField bitfield)> Interesting;
        public List<(IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)> Picks;

        public PiecePickerFilterChecker ()
            : this (null)
        {
        }

        public PiecePickerFilterChecker (IPiecePicker next)
            : base (next)
        {
            Initialised = new List<(BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)> ();
            Interesting = new List<(IPieceRequester peer, BitField bitfield)> ();
            Picks = new List<(IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)> ();
        }

        public override void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
        {
            Initialised.Add ((bitfield, torrentData, requests));
            Next?.Initialise (bitfield, torrentData, requests);
        }

        public override bool IsInteresting (IPieceRequester peer, BitField bitfield)
        {
            Interesting.Add ((peer, bitfield));
            return Next == null ? !bitfield.AllFalse : Next.IsInteresting (peer, bitfield);
        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            Picks.Add ((peer, available.Clone (), new List<IPieceRequester> (otherPeers).AsReadOnly (), count, startIndex, endIndex));
            return Next == null ? null : Next.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);
        }
    }
}
