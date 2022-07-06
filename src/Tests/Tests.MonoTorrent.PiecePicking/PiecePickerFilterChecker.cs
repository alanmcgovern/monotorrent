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
        public List<IPieceRequesterData> Initialised;
        public List<(IRequester peer, ReadOnlyBitField bitfield)> Interesting;
        public List<(IRequester peer, ReadOnlyBitField available, IReadOnlyList<ReadOnlyBitField> otherPeers, int count, int startIndex, int endIndex)> Picks;

        public PiecePickerFilterChecker ()
            : this (null)
        {
        }

        public PiecePickerFilterChecker (IPiecePicker next)
            : base (next)
        {
            Initialised = new List<IPieceRequesterData> ();
            Interesting = new List<(IRequester peer, ReadOnlyBitField bitfield)> ();
            Picks = new List<(IRequester peer, ReadOnlyBitField available, IReadOnlyList<ReadOnlyBitField> otherPeers, int count, int startIndex, int endIndex)> ();
        }

        public override void Initialise (IPieceRequesterData torrentData)
        {
            Initialised.Add (torrentData);
            Next?.Initialise (torrentData);
        }

        public override bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield)
        {
            Interesting.Add ((peer, bitfield));
            return Next == null ? !bitfield.AllFalse : Next.IsInteresting (peer, bitfield);
        }

        public override int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherPeers, int startIndex, int endIndex, Span<PieceSegment> requests)
        {
            Picks.Add ((peer, new ReadOnlyBitField (available), new List<ReadOnlyBitField> (otherPeers.ToArray ()).AsReadOnly (), requests.Length, startIndex, endIndex));
            return Next == null ? 0 : Next.PickPiece (peer, available, otherPeers, startIndex, endIndex, requests);
        }
    }
}
