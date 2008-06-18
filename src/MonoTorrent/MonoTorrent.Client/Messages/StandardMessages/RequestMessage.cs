//
// RequestMessage.cs
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
using MonoTorrent.Client.Messages.FastPeer;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// Represents a "Request" message
    /// </summary>
    public class RequestMessage : PeerMessage
    {
        public const byte MessageId = 6;
        private const int messageLength = 13;

        internal const int MaxSize = 65536;
        internal const int MinSize = 4096;

        #region Private Fields
        private int startOffset;
        private int pieceIndex;
        private int requestLength;
        #endregion


        #region Public Properties

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength
        {
            get { return (messageLength + 4); }
        }

        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }


        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }


        /// <summary>
        /// The length of the block of data
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new RequestMessage
        /// </summary>
        public RequestMessage()
        {
        }


        /// <summary>
        /// Creates a new RequestMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to request</param>
        /// <param name="startOffset">The offset in bytes of the block of data to request</param>
        /// <param name="requestLength">The length of the block of data to request</param>
        public RequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = requestLength;
        }

        #endregion


        #region Methods
        public override void Decode(byte[] buffer, int offset, int length)
        {
            pieceIndex = ReadInt(buffer, offset);
            startOffset = ReadInt(buffer, offset + 4);
            requestLength = ReadInt(buffer, offset + 8);
        }


        public override int Encode(byte[] buffer, int offset)
        {
			int written = offset;
			
			written += Write(buffer, written, messageLength);
			written += Write(buffer, written, MessageId);
			written += Write(buffer, written, pieceIndex);
			written += Write(buffer, written, startOffset);
			written += Write(buffer, written, requestLength);

            CheckWritten(written - offset);
            return written - offset;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            RequestMessage msg = obj as RequestMessage;
            return (msg == null) ? false : (this.pieceIndex == msg.pieceIndex
                                            && this.startOffset == msg.startOffset
                                            && this.requestLength == msg.requestLength);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (this.pieceIndex.GetHashCode() ^ this.requestLength.GetHashCode() ^ this.startOffset.GetHashCode());
        }


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        internal override void Handle(PeerId id)
        {
            // If we are not on the last piece and the user requested a stupidly big/small amount of data
            // we will close the connection
            if (id.TorrentManager.Torrent.Pieces.Count != (this.pieceIndex + 1))
                if (this.requestLength > MaxSize || this.requestLength < MinSize)
                    throw new MessageException("Illegal piece request received. Peer requested " + requestLength.ToString() + " byte");

            PieceMessage m = new PieceMessage(id.TorrentManager, this.PieceIndex, this.startOffset, this.requestLength);

            // If we're not choking the peer, enqueue the message right away
            if (!id.AmChoking)
            {
                id.IsRequestingPiecesCount++;
                id.Enqueue(m);
            }

            // If the peer supports fast peer and the requested piece is one of the allowed pieces, enqueue it
            // otherwise send back a reject request message
            else if (id.SupportsFastPeer && ClientEngine.SupportsFastPeer)
            {
                if (id.AmAllowedFastPieces.Contains(this.pieceIndex))
                {
                    id.IsRequestingPiecesCount++;
                    id.Enqueue(m);
                }
                else
                    id.Enqueue(new RejectRequestMessage(m));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("RequestMessage ");
            sb.Append(" Index ");
            sb.Append(this.pieceIndex);
            sb.Append(" Offset ");
            sb.Append(this.startOffset);
            sb.Append(" Length ");
            sb.Append(this.requestLength);
            return sb.ToString();
        }
        #endregion
    }
}