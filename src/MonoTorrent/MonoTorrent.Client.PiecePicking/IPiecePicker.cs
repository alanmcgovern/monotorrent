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
using System.Linq;

using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.PiecePicking
{
    public static class IPiecePickerExtensions
    {
        public static IList<PieceRequest> CancelRequests (this IPiecePicker picker, IPieceRequester peer)
        {
            return picker.CancelRequests (peer, 0, peer.BitField.Length - 1);
        }

        public static PieceRequest? ContinueAnyExistingRequest (this IPiecePicker picker, IPieceRequester peer, int startIndex, int endIndex)
            => picker.ContinueAnyExistingRequest (peer, startIndex, endIndex, 1);

        public static void Initialise (this IPiecePicker picker, BitField bitfield, ITorrentData torrentData)
        {
            picker.Initialise (bitfield, torrentData, Enumerable.Empty<ActivePieceRequest> ());
        }

        public static PieceRequest? PickPiece (this IPiecePicker picker, IPieceRequester peer, BitField available)
        {
            var result = picker.PickPiece (peer, available, Array.Empty<IPieceRequester> (), 1, 0, available.Length - 1);
            return result?.Single ();
        }

        public static PieceRequest? PickPiece (this IPiecePicker picker, IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers)
        {
            var result = picker.PickPiece (peer, available, otherPeers, 1, 0, available.Length - 1);
            return result?.Single ();
        }

        public static IList<PieceRequest> PickPiece (this IPiecePicker picker, IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count)
        {
            return picker.PickPiece (peer, available, otherPeers, count, 0, available.Length - 1);
        }
    }

    public abstract class PiecePickerFilter : IPiecePicker
    {
        protected IPiecePicker Next { get; }

        protected PiecePickerFilter (IPiecePicker picker)
            => Next = picker;

        public int AbortRequests (IPieceRequester peer)
            => Next.AbortRequests (peer);

        public IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex)
            => Next.CancelRequests (peer, startIndex, endIndex);

        public PieceRequest? ContinueAnyExistingRequest (IPieceRequester peer, int startIndex, int endIndex, int maxDuplicateRequests)
            => Next.ContinueAnyExistingRequest (peer, startIndex, endIndex, maxDuplicateRequests);

        public PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex)
            => Next.ContinueExistingRequest (peer, startIndex, endIndex);

        public int CurrentReceivedCount ()
            => Next.CurrentReceivedCount ();

        public int CurrentRequestCount ()
            => Next.CurrentReceivedCount ();

        public IList<ActivePieceRequest> ExportActiveRequests ()
            => Next.ExportActiveRequests ();

        public virtual void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests)
            => Next.Initialise (bitfield, torrentData, requests);

        public virtual bool IsInteresting (IPieceRequester peer, BitField bitfield)
            => Next.IsInteresting (peer, bitfield);

        public virtual IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex)
            => Next.PickPiece (peer, available, otherPeers, count, startIndex, endIndex);

        public void RequestRejected (IPieceRequester peer, PieceRequest request)
            => Next.RequestRejected (peer, request);

        public bool ValidatePiece (IPieceRequester peer, PieceRequest request, out bool pieceComplete, out IList<IPieceRequester> peersInvolved)
            => Next.ValidatePiece (peer, request, out pieceComplete, out peersInvolved);
    }

    public interface IPiecePicker
    {
        /// <summary>
        /// Cancel all unreceived requests. No further blocks will be requested from this peer.
        /// </summary>
        /// <param name="peer">The peer whose requests will be cancelled.</param>
        /// <returns>The number of requests which were cancelled</returns>
        int AbortRequests (IPieceRequester peer);

        /// <summary>
        /// Cancel all unreceived requests between startIndex and endIndex.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <returns>The list of requests which were cancelled</returns>
        IList<PieceRequest> CancelRequests (IPieceRequester peer, int startIndex, int endIndex);

        /// <summary>
        /// Request any unrequested block from a piece owned by this peer, or any other peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <param name="maxDuplicateRequests">The maximum number of concurrent duplicate requests</param>
        /// <returns></returns>
        PieceRequest? ContinueAnyExistingRequest (IPieceRequester peer, int startIndex, int endIndex, int maxDuplicateRequests);

        /// <summary>
        /// Request the next unrequested block from a piece owned by this peer, within the specified bounds.
        /// </summary>
        /// <param name="peer">The peer to request the block from</param>
        /// <param name="startIndex">The lowest piece index to consider</param>
        /// <param name="endIndex">The highest piece index to consider</param>
        /// <returns></returns>
        PieceRequest? ContinueExistingRequest (IPieceRequester peer, int startIndex, int endIndex);

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
        /// Reset all internal state. Called after <see cref="TorrentManager.StartAsync()"/> or <see cref="TorrentManager.StopAsync()"/> is invoked.
        /// </summary>
        /// <param name="bitfield"></param>
        /// <param name="torrentData"></param>
        /// <param name="requests"></param>
        void Initialise (BitField bitfield, ITorrentData torrentData, IEnumerable<ActivePieceRequest> requests);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="bitfield"></param>
        /// <returns></returns>
        bool IsInteresting (IPieceRequester peer, BitField bitfield);

        /// <summary>
        /// Called when a <see cref="RejectRequestMessage"/> is received from the <paramref name="peer"/> to indicate
        /// the <see cref="PieceRequest"/> will not be fulfilled.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        void RequestRejected (IPieceRequester peer, PieceRequest request);

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
        IList<PieceRequest> PickPiece (IPieceRequester peer, BitField available, IReadOnlyList<IPieceRequester> otherPeers, int count, int startIndex, int endIndex);

        /// <summary>
        /// Called when a <see cref="PieceMessage"/> is received from the <paramref name="peer"/>. Returns true if the
        /// piece was requested from this peer and should be accepted, otherwise returns false if the piece was not requested from this peer and should
        /// be discarded.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="request"></param>
        /// <param name="pieceComplete">True if this was the final block for the piece</param>
        /// <param name="peersInvolved">When <paramref name="pieceComplete"/> is true this is a non-null list of peers used to download the piece. Otherwise this is null.</param>
        /// <returns></returns>
        bool ValidatePiece (IPieceRequester peer, PieceRequest request, out bool pieceComplete, out IList<IPieceRequester> peersInvolved);
    }
}
