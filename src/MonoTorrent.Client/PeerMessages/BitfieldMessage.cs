//
// BitfieldMessage.cs
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
using MonoTorrent.Common;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// 
    /// </summary>
    public class BitfieldMessage : IPeerMessageInternal, IPeerMessage
    {
        public const int MessageId = 5;

        #region Member Variables
        /// <summary>
        /// The bitfield
        /// </summary>
        public BitField BitField
        {
            get { return this.bitField; }
        }
        private BitField bitField;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="length">The length of the bitfield</param>
        public BitfieldMessage(int length)
        {
            this.bitField = new BitField(length);
        }


        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="bitfield">The bitfield to use</param>
        public BitfieldMessage(BitField bitfield)
        {
            this.bitField = bitfield;
        }
        #endregion


        #region Methods
        /// <summary>
        /// Encodes the BitfieldMessage into the supplied buffer
        /// </summary>
        /// <param name="id">The peer who we are about to send the message to</param>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        internal int Encode(byte[] buffer, int offset)
        {
            buffer[offset + 4] = (byte)MessageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bitField.LengthInBytes + 1)), 0, buffer, offset, 4);
            this.bitField.ToByteArray(buffer, offset + 5);

            return (this.BitField.LengthInBytes + 5);
        }


        /// <summary>
        /// Decodes a RequestMessage from the supplied buffer
        /// </summary>
        /// <param name="id">The peer to decode the message from</param>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        internal void Decode(byte[] buffer, int offset, int length)
        {
            this.bitField.FromArray(buffer, offset, length);
        }

#warning Copy this straight into the Peers Bitfield. Slightly more memory efficient :p
        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal void Handle(PeerConnectionID id)
        {
            id.Peer.Connection.BitField = this.bitField;
            id.Peer.IsSeeder = (id.Peer.Connection.BitField.TrueCount == id.Peer.Connection.BitField.Length);

            id.Peer.Connection.IsInterestingToMe = id.TorrentManager.PieceManager.IsInteresting(id);
        }


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (this.bitField.LengthInBytes + 5); }
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "BitfieldMessage";
        }

        public override bool Equals(object obj)
        {
            BitfieldMessage bf = obj as BitfieldMessage;
            if (bf == null)
                return false;

            return this.bitField.Equals(bf.bitField);
        }

        public override int GetHashCode()
        {
            return this.bitField.GetHashCode();
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