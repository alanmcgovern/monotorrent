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


namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// 
    /// </summary>
    class BitfieldMessage : PeerMessage
    {
        internal static readonly byte MessageId = 5;

        #region Member Variables
        /// <summary>
        /// The bitfield
        /// </summary>
        public BitField BitField { get; }

        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="length">The length of the bitfield</param>
        public BitfieldMessage (int length)
        {
            BitField = new BitField (length);
        }


        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="bitfield">The bitfield to use</param>
        public BitfieldMessage (BitField bitfield)
        {
            BitField = bitfield;
        }
        #endregion


        #region Methods

        public override void Decode (byte[] buffer, int offset, int length)
        {
            BitField.FromArray (buffer, offset);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, BitField.LengthInBytes + 1);
            written += Write (buffer, written, MessageId);
            BitField.ToByteArray (buffer, written);
            written += BitField.LengthInBytes;

            return CheckWritten (written - offset);
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => (BitField.LengthInBytes + 5);
        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            return "BitfieldMessage";
        }

        public override bool Equals (object obj)
        {
            if (!(obj is BitfieldMessage bf))
                return false;

            return BitField.Equals (bf.BitField);
        }

        public override int GetHashCode ()
        {
            return BitField.GetHashCode ();
        }
        #endregion
    }
}
