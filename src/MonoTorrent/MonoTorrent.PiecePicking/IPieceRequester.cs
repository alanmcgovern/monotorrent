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


using System.Collections.Generic;

namespace MonoTorrent.PiecePicking
{
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
        void AddRequests (IReadOnlyList<IPeerWithMessaging> peers);

        /// <summary>
        /// Attempts to enqueue more requests for the specified peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="peers"></param>
        void AddRequests (IPeerWithMessaging peer, IReadOnlyList<IPeerWithMessaging> peers);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="blockInfo"></param>
        /// <param name="pieceComplete"></param>
        /// <param name="peersInvolved"></param>
        /// <returns></returns>
        bool ValidatePiece (IPeer peer, BlockInfo blockInfo, out bool pieceComplete, out IList<IPeer> peersInvolved);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="bitField"></param>
        /// <returns></returns>
        bool IsInteresting (IPeer peer, ReadOnlyBitField bitField);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrentData">The files, size and piecelength for the torrent.</param>
        /// <param name="ignorableBitfields"> These bitfields represent pieces which have successfully
        /// downloaded and passed a hash check, pieces which have successfully downloaded but have not hash checked yet or
        /// pieces which have not yet been hash checked by the library and so it is not known whether they should be requested or not.
        /// </param>
        void Initialise (ITorrentManagerInfo torrentData, IReadOnlyList<ReadOnlyBitField> ignorableBitfields);

        IList<BlockInfo> CancelRequests (IPeer peer, int startIndex, int endIndex);

        void RequestRejected (IPeer peer, BlockInfo pieceRequest);

        int CurrentRequestCount ();
    }
}
