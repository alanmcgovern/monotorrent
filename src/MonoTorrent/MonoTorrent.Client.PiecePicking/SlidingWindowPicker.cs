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
    /// <summary>
    /// Generates a sliding window with high, medium, and low priority sets. The high priority set is downloaded first and in order.
    /// The medium and low priority sets are downloaded rarest first.
    /// 
    /// This is intended to be used with a BitTorrent streaming application.
    /// 
    /// The high priority set represents pieces that are needed SOON. This set is updated by calling code, to adapt for events
    /// (e.g. user fast-forwards or seeks, etc.)
    /// </summary>
    public class SlidingWindowPicker : PiecePicker
    {
        #region Member Variables

        // this represents the last byte played in a video player, as the high priority
        // set designates pieces that are needed VERY SOON
        int highPrioritySetStart;           // gets updated by calling code, or as pieces get downloaded

        /// <summary>
        /// Gets or sets first "high priority" piece. The n pieces after this will be requested in-order,
        /// the rest of the file will be treated rarest-first
        /// </summary>
        public int HighPrioritySetStart {
            get => highPrioritySetStart;
            set {
                if (highPrioritySetStart < value)
                    highPrioritySetStart = value;
            }
        }

        /// <summary>
        /// Gets or sets the size, in pieces, of the high priority set.
        /// </summary>
        public int HighPrioritySetSize { get; set; }

        public int MediumPrioritySetStart => HighPrioritySetStart + HighPrioritySetSize + 1;

        /// <summary>
        /// This is the size ratio between the medium and high priority sets. Equivalent to mu in Tribler's Give-to-get paper.
        /// Default value is 4.
        /// </summary>
        public int MediumToHighRatio { get; set; } = 4;

        /// <summary>
        /// Read-only value for size of the medium priority set. To set the medium priority size, use MediumToHighRatio.
        /// </summary>
        public int MediumPrioritySetSize => HighPrioritySetSize * MediumToHighRatio;

        #endregion Member Variables

        #region Constructors

        /// <summary>
        /// Empty constructor for changing piece pickers
        /// </summary>
        public SlidingWindowPicker (PiecePicker picker)
            : base (picker)
        {
        }


        /// <summary>
        /// Creates a new piece picker with support for prioritization of files. The sliding window will be positioned to the start
        /// of the first file to be downloaded
        /// </summary>
        /// <param name="picker">The picker which requests should be forwarded to</param>
        /// <param name="highPrioritySetSize">Size of high priority set</param>
        internal SlidingWindowPicker (PiecePicker picker, int highPrioritySetSize)
            : this (picker, highPrioritySetSize, 4)
        {
        }


        /// <summary>
        /// Create a new SlidingWindowPicker with the given set sizes. The sliding window will be positioned to the start
        /// of the first file to be downloaded
        /// </summary>
        /// <param name="picker">The picker which requests should be forwarded to</param>
        /// <param name="highPrioritySetSize">Size of high priority set</param>
        /// <param name="mediumToHighRatio">Size of medium priority set as a multiple of the high priority set size</param>
        internal SlidingWindowPicker (PiecePicker picker, int highPrioritySetSize, int mediumToHighRatio)
            : base (picker)
        {
            HighPrioritySetSize = highPrioritySetSize;
            MediumToHighRatio = mediumToHighRatio;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="bitfield"></param>
        /// <param name="torrentData"></param>
        /// <param name="requests"></param>
        public override void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<Piece> requests)
        {
            base.Initialise (bitfield, torrentData, requests);

            // set the high priority set start to the beginning of the first file that we have to download
            foreach (var file in torrentData.Files) {
                if (file.Priority == Priority.DoNotDownload)
                    highPrioritySetStart = file.EndPieceIndex;
                else
                    break;
            }
        }

        #endregion


        #region Methods

        public override IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
        {
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

        #endregion
    }
}
