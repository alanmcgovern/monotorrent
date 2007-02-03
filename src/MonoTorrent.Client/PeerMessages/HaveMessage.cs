//
// HaveMessage.cs
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
using System.Net;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// Represents a "Have" message
    /// </summary>
    public class HaveMessage : IPeerMessageInternal, IPeerMessage
    {
        public const int MessageId = 4;
        private const int messageLength = 5;


        #region Member Variables
        /// <summary>
        /// The index of the piece that you "have"
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        public HaveMessage()
        {
        }


        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece that you "have"</param>
        public HaveMessage(int pieceIndex)
        {
            this.pieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        /// <summary>
        /// Encodes the HaveMessage into the supplied buffer
        /// </summary>
        /// <param name="id">The peer who we are about to send the message to</param>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        internal int Encode(byte[] buffer, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            buffer[offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(pieceIndex)), 0, buffer, offset + 5, 4);

            return (messageLength + 4);
        }


        /// <summary>
        /// Decodes a HaveMessage from the supplied buffer
        /// </summary>
        /// <param name="id">The peer to decode the message from</param>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        internal void Decode(byte[] buffer, int offset, int length)
        {
            this.pieceIndex = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal void Handle(PeerConnectionID id)
        {
            // First set the peers bitfield to true for that piece
            id.Peer.Connection.BitField[this.pieceIndex] = true;


            // We can do a fast check to see if the peer is interesting or not when we receive a Have Message.
            // If the peer just received a piece we don't have, he's interesting. Otherwise his state is unchanged
            if (!id.TorrentManager.Bitfield[this.pieceIndex])
            {
                id.Peer.Connection.IsInterestingToMe = true;
                id.TorrentManager.SetAmInterestedStatus(id);
            }

            // Fastcheck to see if a peer is a seeder or not
            id.Peer.IsSeeder = (id.Peer.Connection.BitField.TrueCount == id.Peer.Connection.BitField.Length);
        }


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + 4); }
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "HaveMessage";
        }

        public override bool Equals(object obj)
        {
            HaveMessage msg = obj as HaveMessage;

            if (msg == null)
                return false;

            return (this.pieceIndex == msg.pieceIndex);
        }

        public override int GetHashCode()
        {
            return this.pieceIndex.GetHashCode();
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