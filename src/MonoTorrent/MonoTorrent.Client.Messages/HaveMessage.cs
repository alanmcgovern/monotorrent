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


namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// Represents a "Have" message
    /// </summary>
    class HaveMessage : PeerMessage
    {
        internal static readonly byte MessageId = 4;
        const int messageLength = 5;


        #region Member Variables
        /// <summary>
        /// The index of the piece that you "have"
        /// </summary>
        public int PieceIndex { get; set; }

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        public HaveMessage ()
        {
        }


        /// <summary>
        /// Creates a new HaveMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece that you "have"</param>
        public HaveMessage (int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }
        #endregion


        #region Methods
        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, messageLength);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, PieceIndex);

            return CheckWritten (written - offset);
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt (buffer, offset);
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => (messageLength + 4);
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("HaveMessage ");
            sb.Append (" Index ");
            sb.Append (PieceIndex);
            return sb.ToString ();
        }

        public override bool Equals (object obj)
        {
            if (!(obj is HaveMessage msg))
                return false;

            return (PieceIndex == msg.PieceIndex);
        }

        public override int GetHashCode ()
        {
            return PieceIndex.GetHashCode ();
        }
        #endregion
    }
}