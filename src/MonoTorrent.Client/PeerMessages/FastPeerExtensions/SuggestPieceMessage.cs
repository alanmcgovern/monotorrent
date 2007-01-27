//
// SuggestPieceMessage.cs
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
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
    // FIXME: The only use for a SuggestPiece message is for when i load a piece into a Disk Cache and want to make use for it
    public class SuggestPieceMessage : IPeerMessageInternal, IPeerMessage
    {
        public const byte MessageId = 0x0D;
        private readonly int messageLength = 5;

        #region Member Variables
        /// <summary>
        /// The index of the suggested piece to request
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        public SuggestPieceMessage()
        {
        }


        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        /// <param name="pieceIndex">The suggested piece to download</param>
        public SuggestPieceMessage(int pieceIndex)
        {
            this.pieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        internal int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");

            buffer[offset + 4] = MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.pieceIndex)), 0, buffer, offset + 5, 4);
            return this.messageLength + 4;
        }


        internal void Decode(byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");

            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }


        internal void Handle(PeerConnectionID id)
        {
            id.Peer.Connection.SuggestedPieces.Add(this.pieceIndex);
        }


        public int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            SuggestPieceMessage msg = obj as SuggestPieceMessage;
            if (msg == null)
                return false;

            return this.pieceIndex == msg.pieceIndex;
        }

        public override int GetHashCode()
        {
            return this.pieceIndex.GetHashCode();
        }

        public override string ToString()
        {
            return "SuggestPieceMessage";
        }
        #endregion


        #region IPeerMessageInternal Explicit Calls

        int IPeerMessageInternal.Encode(byte[] buffer, int offset)
        {
            return this.Encode(buffer, offset);
        }

        void IPeerMessageInternal.Decode(byte[] buffer, int offset, int length)
        {
            this.Decode(buffer, offset, length);
        }

        void IPeerMessageInternal.Handle(PeerConnectionID id)
        {
            this.Handle(id);
        }

        int IPeerMessageInternal.ByteLength
        {
            get { return this.ByteLength; }
        }

        #endregion
    }
}
