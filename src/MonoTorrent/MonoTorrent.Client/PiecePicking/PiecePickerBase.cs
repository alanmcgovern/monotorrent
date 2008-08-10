//
// IPiecePicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    public abstract class PiecePickerBase
    {
        #region Private Fields

        protected BitField myBitfield;
        private bool linearPickingEnabled;
        protected BitField unhashedPieces;

        #endregion Private Fields

        #region Properties

        public BitField MyBitField
        {
            get { return this.myBitfield; }
        }

        public bool LinearPickingEnabled
        {
            get { return linearPickingEnabled; }
            set { linearPickingEnabled = value; }
        }

        public BitField UnhashedPieces
        {
            get { return unhashedPieces; }
        }

        #endregion Properties

        #region Abstract Methods

        public abstract int CurrentRequestCount();
        public abstract List<Piece> ExportActiveRequests();
        public abstract bool IsInteresting(PeerId id);
        public abstract void Initialise(BitField ownBitfield, TorrentFile[] files, IEnumerable<Piece> requests, BitField unhashedPieces);
        public abstract RequestMessage PickPiece(PeerId id, List<PeerId> otherPeers);
        public abstract MessageBundle PickPiece(PeerId id, List<PeerId> otherPeers, int count);
        public abstract void ReceivedChokeMessage(PeerId id);
        public abstract void ReceivedRejectRequest(PeerId id, RejectRequestMessage message);
        public abstract PieceEvent ReceivedPieceMessage(BufferedIO data);
        public abstract void RemoveRequests(PeerId id);
        public abstract void Reset();

        #endregion
    }
}
