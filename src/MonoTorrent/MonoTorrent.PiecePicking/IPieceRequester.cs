//
// IPieceRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
    public interface IPieceRequesterData
    {
        IList<ITorrentManagerFile> Files { get; }
        int SegmentsPerPiece (int piece);
        int ByteOffsetToPieceIndex (long byteOffset);
        int BytesPerPiece (int piece);
        int PieceCount { get; }
        int PieceLength { get; }
    }

    public interface IMessageEnqueuer
    {
        void EnqueueRequest (IRequester peer, PieceSegment block);
        void EnqueueRequests (IRequester peer, Span<PieceSegment> blocks);
        void EnqueueCancellation (IRequester peer, PieceSegment segment);
        void EnqueueCancellations (IRequester peer, Span<PieceSegment> segments);
    }

    /// <summary>
    /// Allows an IPiecePicker implementation to create piece requests for
    /// specific peers and then add them to the peers message queue. If the
    /// limits on maximum concurrent piece requests are ignored
    /// </summary>
    public interface IPieceRequester
    {
        /// <summary>
        /// Should return <see langword="true"/> if the underlying piece picking algorithm
        /// has entered 'endgame mode' as defined by the bittorrent specification.
        /// </summary>
        bool InEndgameMode { get; }

        /// <summary>
        /// Should enqueue piece requests for any peer who is has capacity.
        /// </summary>
        /// <param name="peers"></param>
        void AddRequests (ReadOnlySpan<(IRequester Peer, ReadOnlyBitField Available)> peers);

        /// <summary>
        /// Attempts to enqueue more requests for the specified peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="available"></param>
        /// <param name="peers"></param>
        void AddRequests (IRequester peer, ReadOnlyBitField available, ReadOnlySpan<ReadOnlyBitField> peers);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="pieceSegment"></param>
        /// <param name="pieceComplete"></param>
        /// <param name="peersInvolved"></param>
        /// <returns></returns>
        bool ValidatePiece (IRequester peer, PieceSegment pieceSegment, out bool pieceComplete, HashSet<IRequester> peersInvolved);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="bitField"></param>
        /// <returns></returns>
        bool IsInteresting (IRequester peer, ReadOnlyBitField bitField);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrentData">The files, size and piecelength for the torrent.</param>
        /// <param name="enqueuer">Enqueues request, or cancellation, messages with the peer</param>
        /// <param name="ignorableBitfields"> These bitfields represent pieces which have successfully
        /// downloaded and passed a hash check, pieces which have successfully downloaded but have not hash checked yet or
        /// pieces which have not yet been hash checked by the library and so it is not known whether they should be requested or not.
        /// </param>
        void Initialise (IPieceRequesterData torrentData, IMessageEnqueuer enqueuer, ReadOnlySpan<ReadOnlyBitField> ignorableBitfields);

        void CancelRequests (IRequester peer, int startIndex, int endIndex);

        void RequestRejected (IRequester peer, PieceSegment pieceRequest);

        int CurrentRequestCount ();
    }
}
