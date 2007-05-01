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
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal abstract class PiecePickerBase
    {
        #region Private Fields

        protected BitField myBitfield;

        #endregion Private Fields

        #region Properties

        /// <summary>
        /// The bitfield for the torrent
        /// </summary>
        public BitField MyBitField
        {
            get { return this.myBitfield; }
        }

        #endregion Properties

        #region Abstract Methods

        public abstract int CurrentRequestCount();
        public abstract bool IsInteresting(PeerConnectionID id);
        public abstract RequestMessage PickPiece(PeerConnectionID id, PeerConnectionIDCollection otherPeers);
        public abstract void ReceivedChokeMessage(PeerConnectionID id);
        public abstract void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage message);
        public abstract PieceEvent ReceivedPieceMessage(PeerConnectionID id, byte[] buffer, PieceMessage message);
        public abstract void RemoveRequests(PeerConnectionID id);

        #endregion

        #region Methods

        internal static int GetBlockIndex(Block[] blocks, int blockStartOffset, int blockLength)
        {
            for (int i = 0; i < blocks.Length; i++)
                if (blocks[i].StartOffset == blockStartOffset && blocks[i].RequestLength == blockLength)
                    return i;

            return -1;
        } 

        internal static Piece GetPieceFromIndex(PieceCollection pieces, int pieceIndex)
        {
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].Index == pieceIndex)
                    return pieces[i];

            return null;
        }

        #endregion Methods
    }
}
