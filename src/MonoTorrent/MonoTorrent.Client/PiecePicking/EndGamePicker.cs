//
// EndgamePicker.cs
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
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.FastPeer;

namespace MonoTorrent.Client.PiecePicking
{
    public class EndGamePicker : PiecePicker
    {
        private List<KeyValuePair<Peer, Block>> requests;
        TimeSpan timeout;

        public override TimeSpan Timeout
        {
            get { return timeout; }
            set { timeout = value; }
        }

        public EndGamePicker()
            : base(null)
        {
            timeout = DefaultTimeout;
        }

        public override void CancelTimedOutRequests()
        {
            // no timeouts
        }

        public override int CurrentRequestCount()
        {
            return requests.Count;
        }

        public override List<Piece> ExportActiveRequests()
        {
            // Return a list generated from the requests
            return null;
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests, BitField unhashedPieces)
        {
            // initialise the requests list from the IEnumarble request
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int startIndex, int endIndex, int count)
        {
            // Find a block we havent already requested a bunch of times
            return null;
        }

        public override void Reset()
        {
            // Though if you reset an EndGamePicker it really means that you should be using a regular picker now
            requests.Clear();
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            base.CancelRequest(peer, piece, startOffset, length);
        }

        public override void CancelRequests(PeerId peer)
        {
            base.CancelRequests(peer);
        }

        public override bool ValidatePiece(PeerId peer, int piece, int startOffset, int length)
        {
            return base.ValidatePiece(peer, piece, startOffset, length);
        }
    }
}
