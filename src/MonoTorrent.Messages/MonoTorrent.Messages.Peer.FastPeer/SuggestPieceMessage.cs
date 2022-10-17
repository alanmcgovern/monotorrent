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
using System.Text;

namespace MonoTorrent.Messages.Peer.FastPeer
{
    // FIXME: The only use for a SuggestPiece message is for when i load a piece into a Disk Cache and want to make use for it
    public class SuggestPieceMessage : PeerMessage, IRentable, IFastPeerMessage
    {
        internal const byte MessageId = 0x0D;
        readonly int messageLength = 5;

        #region Member Variables

        public override int ByteLength => messageLength + 4;

        /// <summary>
        /// The index of the suggested piece to request
        /// </summary>
        public int PieceIndex { get; private set; }
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        public SuggestPieceMessage ()
        {
        }


        /// <summary>
        /// Creates a new SuggestPiece message
        /// </summary>
        /// <param name="pieceIndex">The suggested piece to download</param>
        public SuggestPieceMessage (int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, PieceIndex);

            return written - buffer.Length;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PieceIndex = ReadInt (ref buffer);
        }

        #endregion


        #region Overidden Methods
        public override bool Equals (object? obj)
        {
            return (obj as SuggestPieceMessage)?.PieceIndex == PieceIndex;
        }

        public override int GetHashCode ()
        {
            return PieceIndex.GetHashCode ();
        }

        public override string ToString ()
        {
            var sb = new StringBuilder (24);
            sb.Append ("Suggest Piece");
            sb.Append (" Index: ");
            sb.Append (PieceIndex);
            return sb.ToString ();
        }
        #endregion
    }
}
