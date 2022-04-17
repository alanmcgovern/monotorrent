//
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


using System;
using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    class PiecePickerFilterChecker : PiecePickerFilter
    {
        public List<ITorrentManagerInfo> Initialised;
        public List<(IPeer peer, ReadOnlyBitField bitfield)> Interesting;
        public List<(IPeer peer, ReadOnlyBitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex)> Picks;

        public PiecePickerFilterChecker ()
            : this (null)
        {
        }

        public PiecePickerFilterChecker (IPiecePicker next)
            : base (next)
        {
            Initialised = new List<ITorrentManagerInfo> ();
            Interesting = new List<(IPeer peer, ReadOnlyBitField bitfield)> ();
            Picks = new List<(IPeer peer, ReadOnlyBitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex)> ();
        }

        public override void Initialise (ITorrentManagerInfo torrentData)
        {
            Initialised.Add (torrentData);
            Next?.Initialise (torrentData);
        }

        public override bool IsInteresting (IPeer peer, ReadOnlyBitField bitfield)
        {
            Interesting.Add ((peer, bitfield));
            return Next == null ? !bitfield.AllFalse : Next.IsInteresting (peer, bitfield);
        }

        public override int PickPiece (IPeer peer, ReadOnlyBitField available, IReadOnlyList<IPeer> otherPeers, int startIndex, int endIndex, Span<BlockInfo> requests)
        {
            Picks.Add ((peer, new ReadOnlyBitField (available), new List<IPeer> (otherPeers).AsReadOnly (), requests.Length, startIndex, endIndex));
            return Next == null ? 0 : Next.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);
        }
    }
}
