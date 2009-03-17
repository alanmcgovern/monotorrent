//
// NullPicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class NullPicker : PiecePicker
    {
        public NullPicker()
            : base(null)
        {

        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            
        }

        public override void CancelRequests(PeerId peer)
        {
            
        }

        public override void CancelTimedOutRequests()
        {
            
        }

        public override int CurrentRequestCount()
        {
            return 0;
        }

        public override List<Piece> ExportActiveRequests()
        {
            return new List<Piece>();
        }

        public override void Initialise(MonoTorrent.Common.BitField bitfield, MonoTorrent.Common.TorrentFile[] files, IEnumerable<Piece> requests)
        {
            
        }
        
        public override bool IsInteresting(MonoTorrent.Common.BitField bitfield)
        {
            return false;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            return null;
        }

        public override void Reset()
        {
            
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            piece = null;
            return false;
        }
    }
}
