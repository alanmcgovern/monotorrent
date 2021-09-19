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

namespace MonoTorrent.PiecePicking
{
    public static class IPiecePickerExtensions
    {
        public static IList<BlockInfo> CancelRequests (this IPiecePicker picker, IPeer peer)
        {
            return picker.CancelRequests (peer, 0, peer.BitField.Length - 1);
        }

        public static BlockInfo? ContinueAnyExistingRequest (this IPiecePicker picker, IPeer peer, int startIndex, int endIndex)
            => picker.ContinueAnyExistingRequest (peer, startIndex, endIndex, 1);

        public static BlockInfo? PickPiece (this IPiecePicker picker, IPeer peer, BitField available)
        {
            var result = picker.PickPiece (peer, available, Array.Empty<IPeer> (), 1, 0, available.Length - 1);
            return result?.Single ();
        }

        public static BlockInfo? PickPiece (this IPiecePicker picker, IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers)
        {
            var result = picker.PickPiece (peer, available, otherPeers, 1, 0, available.Length - 1);
            return result?.Single ();
        }

        public static IList<BlockInfo> PickPiece (this IPiecePicker picker, IPeer peer, BitField available, IReadOnlyList<IPeer> otherPeers, int count)
        {
            return picker.PickPiece (peer, available, otherPeers, count, 0, available.Length - 1);
        }
    }
}
