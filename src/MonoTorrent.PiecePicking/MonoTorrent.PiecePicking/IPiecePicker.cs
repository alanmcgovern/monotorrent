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


using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
    public interface IPiecePicker
    {
        /// <summary>
        /// Cancel all unreceived requests. No further blocks will be requested from this peer.
        /// </summary>
        /// <param name="peer">The peer whose requests will be cancelled.</param>
        /// <returns>The number of requests which were cancelled</returns>
        int AbortRequests (IPeer peer);

        /// <summary>
        /// Cancel all unreceived requests between startIndex and endIndex.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <returns>The list of requests which were cancelled</returns>
        IList<BlockInfo> CancelRequests (IPeer peer, int startIndex, int endIndex);

        /// <summary>
        /// Request any unrequested block from a piece owned by this peer, or any other peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <param name="maxDuplicateRequests">The maximum number of concurrent duplicate requests</param>
        /// <returns></returns>
        BlockInfo? ContinueAnyExistingRequest (IPeer peer, int startIndex, int endIndex, int maxDuplicateRequests);

        /// <summary>
        /// Request the next unrequested block from a piece owned by this peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <returns></returns>
        BlockInfo? ContinueExistingRequest (IPeer peer, int startIndex, int endIndex);

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
        void Initialise (ITorrentData torrentData);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="bitfield"></param>
        /// <returns></returns>
        bool IsInteresting (IPeer peer, BitField bitfield);

        /// <summary>
        /// Called when a piece request has been rejected by a <paramref name="peer"/>, which indicates
        /// the <see cref="BlockInfo"/> will not be fulfilled.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        void RequestRejected (IPeer peer, BlockInfo request);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="available"></param>
        /// <param name="otherPeers"></param>
        /// <param name="count"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        IList<BlockInfo> PickPiece (IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers, int count, int startIndex, int endIndex);

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
        bool ValidatePiece (IPeer peer, BlockInfo request, out bool pieceComplete, out IList<IPeer> peersInvolved);
    }
}
