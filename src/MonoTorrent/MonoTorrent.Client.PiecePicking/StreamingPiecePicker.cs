//
// SlidingWindowPicker.cs
//
// Authors:
//   Karthik Kailash    karthik.l.kailash@gmail.com
//   David Sanghera     dsanghera@gmail.com
//
// Copyright (C) 2006 Karthik Kailash, David Sanghera
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

namespace MonoTorrent.Client.PiecePicking
{
    class StreamingPiecePicker : PiecePicker
    {
        private int ratio = 4;                      // ratio from medium priority to high priority set size
        private int highPrioritySetSize;            // size of high priority set, in pieces
        // this represents the last byte played in a video player, as the high priority
        // set designates pieces that are needed VERY SOON

        /// <summary>
        /// Gets or sets first "high priority" piece. The n pieces after this will be requested in-order,
        /// the rest of the file will be treated rarest-first
        /// </summary>
        public int HighPrioritySetStart { get; set; }

        /// <summary>
        /// Gets or sets the size, in pieces, of the high priority set.
        /// </summary>
        public int HighPrioritySetSize {
            get { return this.highPrioritySetSize; }
            set { this.highPrioritySetSize = value; }
        }

        public int MediumPrioritySetStart {
            get { return HighPrioritySetStart + HighPrioritySetSize + 1; }
        }

        /// <summary>
        /// This is the size ratio between the medium and high priority sets. Equivalent to mu in Tribler's Give-to-get paper.
        /// Default value is 4.
        /// </summary>
        public int MediumToHighRatio {
            get { return ratio; }
            set { ratio = value; }
        }

        /// <summary>
        /// Read-only value for size of the medium priority set. To set the medium priority size, use MediumToHighRatio.
        /// </summary>
        public int MediumPrioritySetSize {
            get { return this.highPrioritySetSize * ratio; }
        }

        int PieceLength { get; }

        bool CancelPendingRequests { get; set; }

        /// <summary>
        /// Empty constructor for changing piece pickers
        /// </summary>
        public StreamingPiecePicker (PiecePicker picker, int pieceLength)
            : base (picker)
        {
            PieceLength = pieceLength;
            HighPrioritySetSize = 10;
        }

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
            if (CancelPendingRequests) {
                foreach (var p in otherPeers)
                    CancelRequests (p);
                CancelRequests (peer);
                CancelPendingRequests = false;
            }

            IList<PieceRequest> bundle;
            int start, end;

            if (HighPrioritySetStart >= startIndex && HighPrioritySetStart <= endIndex) {
                start = HighPrioritySetStart;
                end = Math.Min (endIndex, HighPrioritySetStart + HighPrioritySetSize - 1);
                if ((bundle = base.PickPiece (peer, available, otherPeers, count, start, end)) != null)
                    return bundle;
            }

            if (MediumPrioritySetStart >= startIndex && MediumPrioritySetStart <= endIndex) {
                start = MediumPrioritySetStart;
                end = Math.Min (endIndex, MediumPrioritySetStart + MediumPrioritySetSize - 1);
                if ((bundle = base.PickPiece (peer, available, otherPeers, count, start, end)) != null)
                    return bundle;
            }

            return base.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);
        }

        internal void UpdatePriority (TorrentFile file, long position)
        {
            HighPrioritySetStart = file.StartPieceIndex + (int) ((file.StartPieceOffset + position) / PieceLength);
            CancelPendingRequests = true;
        }
    }
}
