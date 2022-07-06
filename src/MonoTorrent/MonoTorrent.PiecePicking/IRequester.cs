//
// IPeer.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
    public interface IRequester
    {
        int AmRequestingPiecesCount { get; set; }
        bool CanRequestMorePieces { get; }
        long DownloadSpeed { get; }
        List<int> IsAllowedFastPieces { get; }
        bool IsChoking { get; }
        int MaxPendingRequests { get; }
        int RepeatedHashFails { get; }
        List<int> SuggestedPieces { get; }
        bool SupportsFastPeer { get; }

        // FIXME: We need to support 3rd party implementations of 'IPiecePicker' calling
        // CancelRequest if the FastPeer extensions are supported. This includes enqueuing
        // and sending the appropriate Cancel messages.
        bool CanCancelRequests { get; }

        /// <summary>
        /// Returns the number of blocks to request. If the value is greater than 1 it will be
        /// rounded up to 1 full piece.
        /// </summary>
        /// <param name="pieceLength"></param>
        /// <returns></returns>
        int PreferredRequestAmount (int pieceLength);
    }
}
