//
// RejectRequestMessage.cs
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


using System.Text;

using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.FastPeer
{
    class RejectRequestMessage : PeerMessage, IFastPeerMessage
    {
        internal static readonly byte MessageId = 0x10;
        public readonly int messageLength = 13;

        #region Member Variables
        public override int ByteLength => messageLength + 4;

        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset { get; private set; }

        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex { get; private set; }

        /// <summary>
        /// The length of the block of data
        /// </summary>
        public int RequestLength { get; private set; }
        #endregion


        #region Constructors
        public RejectRequestMessage ()
        {
        }


        public RejectRequestMessage (PieceMessage message)
            : this (message.PieceIndex, message.StartOffset, message.RequestLength)
        {
        }

        public RejectRequestMessage (RequestMessage message)
            : this (message.PieceIndex, message.StartOffset, message.RequestLength)
        {
        }

        public RejectRequestMessage (int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
        }
        #endregion


        #region Methods
        public override int Encode (byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException ("Message encoding not supported");

            int written = offset;

            written += Write (buffer, written, messageLength);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, PieceIndex);
            written += Write (buffer, written, StartOffset);
            written += Write (buffer, written, RequestLength);

            return CheckWritten (written - offset);
        }


        public override void Decode (byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException ("Message decoding not supported");

            PieceIndex = ReadInt (buffer, ref offset);
            StartOffset = ReadInt (buffer, ref offset);
            RequestLength = ReadInt (buffer, ref offset);
        }
        #endregion


        #region Overidden Methods
        public override bool Equals (object obj)
        {
            if (!(obj is RejectRequestMessage msg))
                return false;

            return (PieceIndex == msg.PieceIndex
                && StartOffset == msg.StartOffset
                && RequestLength == msg.RequestLength);
        }


        public override int GetHashCode ()
        {
            return (PieceIndex.GetHashCode ()
                    ^ RequestLength.GetHashCode ()
                    ^ StartOffset.GetHashCode ());
        }


        public override string ToString ()
        {
            var sb = new StringBuilder (24);
            sb.Append ("Reject Request");
            sb.Append (" Index: ");
            sb.Append (PieceIndex);
            sb.Append (" Offset: ");
            sb.Append (StartOffset);
            sb.Append (" Length ");
            sb.Append (RequestLength);
            return sb.ToString ();
        }
        #endregion
    }
}
