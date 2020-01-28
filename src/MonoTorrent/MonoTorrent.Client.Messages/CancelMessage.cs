//
// CancelMessage.cs
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


namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// 
    /// </summary>
    class CancelMessage : PeerMessage
    {
        private const int messageLength = 13;
        internal static readonly byte MessageId = 8;


        #region Member Variables
        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex { get; set; }


        /// <summary>
        /// The offset in bytes of the block of data
        /// </summary>
        public int StartOffset { get; set; }


        /// <summary>
        /// The length in bytes of the block of data
        /// </summary>
        public int RequestLength { get; set; }

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        public CancelMessage ()
        {
        }


        /// <summary>
        /// Creates a new CancelMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to cancel</param>
        /// <param name="startOffset">The offset in bytes of the block of data to cancel</param>
        /// <param name="requestLength">The length in bytes of the block of data to cancel</param>
        public CancelMessage (int pieceIndex, int startOffset, int requestLength)
        {
            this.PieceIndex = pieceIndex;
            this.StartOffset = startOffset;
            this.RequestLength = requestLength;
        }
        #endregion


        #region Methods
        public override int Encode (byte[] buffer, int offset)
        {
            var written = offset;

            written += Write (buffer, written, messageLength);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, PieceIndex);
            written += Write (buffer, written, StartOffset);
            written += Write (buffer, written, RequestLength);

            return CheckWritten (written - offset);
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt (buffer, ref offset);
            StartOffset = ReadInt (buffer, ref offset);
            RequestLength = ReadInt (buffer, ref offset);
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength {
            get { return (messageLength + 4); }
        }
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("CancelMessage ");
            sb.Append (" Index ");
            sb.Append (this.PieceIndex);
            sb.Append (" Offset ");
            sb.Append (this.StartOffset);
            sb.Append (" Length ");
            sb.Append (this.RequestLength);
            return sb.ToString ();
        }

        public override bool Equals (object obj)
        {
            if (!(obj is CancelMessage msg))
                return false;

            return (this.PieceIndex == msg.PieceIndex
                    && this.StartOffset == msg.StartOffset
                    && this.RequestLength == msg.RequestLength);
        }

        public override int GetHashCode ()
        {
            return (this.PieceIndex.GetHashCode ()
                ^ this.RequestLength.GetHashCode ()
                ^ this.StartOffset.GetHashCode ());
        }
        #endregion
    }
}