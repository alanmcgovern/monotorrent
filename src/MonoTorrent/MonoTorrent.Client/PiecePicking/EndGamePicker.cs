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

        public EndGamePicker()
            : base(null)
        {
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

        public override bool IsInteresting(PeerId id)
        {
            // It'd be much faster to keep a list of pieces which are remaining and
            // just check those individual indices.

            BitField b = id.BitField.And(id.TorrentManager.Bitfield);
            if (b.AllFalse)
                return false;

            int index = 0;
            //while ((index = b.FirstTrue(index, b.Length)) != -1)
            //    if (!AlreadyRequestedThreeTimes(index))
            //        return true;
            return false;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int startIndex, int endIndex, int count)
        {
            // Find a block we havent already requested a bunch of times
            return null;
        }

        public override void ReceivedChokeMessage(PeerId id)
        {
            //requests.RemoveAll(delegate(KeyValuePair<Peer, Block> b) { return b.Key == id; });
        }

        public override PieceEvent ReceivedPieceMessage(BufferedIO data)
        {
            //requests.Exists(delegate(KeyValuePair<Peer, Block> b) { return b.Key == id && b.Value.Block == data.Block; });
            // We received a piece we requested, so we can write this to disk

            //base.ReceivedGoodPiece(data);
            return base.ReceivedPieceMessage(data);
        }

        public override void ReceivedRejectRequest(PeerId id, RejectRequestMessage message)
        {
            //requests.RemoveAll(delegate(KeyValuePair<Peer, Block> b) { return b.Key == id && b.Value.Block == message.Block; });
        }

        public override void RemoveRequests(PeerId id)
        {
            //requests.RemoveAll(delegate(KeyValuePair<Peer, Block> b) { return b.Key == id; });
        }

        public override void Reset()
        {
            // Though if you reset an EndGamePicker it really means that you should be using a regular picker now
            requests.Clear();
        }
    }
}
