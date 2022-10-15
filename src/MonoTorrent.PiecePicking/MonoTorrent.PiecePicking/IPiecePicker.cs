//
// IPiecePicker.cs
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


using System;
using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    public interface IPiecePicker
    {
        /// <summary>
        /// Cancel all unreceived requests between startIndex and endIndex.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <param name="cancellations"></param>
        /// <returns>The number of entries written to the span</returns>
        int CancelRequests (IRequester peer, int startIndex, int endIndex, Span<PieceSegment> cancellations);

        /// <summary>
        /// Request any unrequested block from a piece owned by this peer, or any other peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="available"></param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <param name="maxDuplicateRequests">The maximum number of concurrent duplicate requests</param>
        /// <returns></returns>
        /// <param name="segment"></param>
        bool ContinueAnyExistingRequest (IRequester peer, ReadOnlyBitField available, int startIndex, int endIndex, int maxDuplicateRequests, out PieceSegment segment);

        /// <summary>
        /// Request the next unrequested block from a piece owned by this peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <param name="segment">If an existing block is successfully continued, the details for that block will be set here</param>
        /// <returns></returns>
        bool ContinueExistingRequest (IRequester peer, int startIndex, int endIndex, out PieceSegment segment);

        /// <summary>
        /// Returns the number of blocks which have been received f pieces currently being requested.
        /// </summary>
        /// <returns></returns>
        int CurrentReceivedCount ();

        /// <summary>
        /// Returns the number of pieces currently being requested.
        /// </summary>
        /// <returns></returns>
        int CurrentRequestCount ();

        /// <summary>
        /// Returns a list of all
        /// </summary>
        /// <returns></returns>
        IList<ActivePieceRequest> ExportActiveRequests ();

        /// <summary>
        /// Reset all internal state.
        /// </summary>
        /// <param name="torrentData"></param>
        void Initialise (IPieceRequesterData torrentData);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="bitfield"></param>
        /// <returns></returns>
        bool IsInteresting (IRequester peer, ReadOnlyBitField bitfield);

        /// <summary>
        /// Called when a piece request has been rejected by a <paramref name="peer"/>, which indicates
        /// the <see cref="PieceSegment"/> will not be fulfilled.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        void RequestRejected (IRequester peer, PieceSegment request);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="available"></param>
        /// <param name="otherAvailable"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <param name="requests"></param>
        /// <returns></returns>
        int PickPiece (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> otherAvailable, int startIndex, int endIndex, Span<PieceSegment> requests);

        /// <summary>
        /// Called when a piece is received from the <paramref name="peer"/>. Returns true if the
        /// piece was requested from this peer and should be accepted, otherwise returns false if the piece was not requested from this peer and should
        /// be discarded.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        /// <param name="pieceComplete">True if this was the final block for the piece</param>
        /// <param name="peersInvolved">When <paramref name="pieceComplete"/> is true this is a non-null list of peers used to download the piece. Otherwise this is null.</param>
        /// <returns></returns>
        bool ValidatePiece (IRequester peer, PieceSegment request, out bool pieceComplete, HashSet<IRequester> peersInvolved);
    }
}
