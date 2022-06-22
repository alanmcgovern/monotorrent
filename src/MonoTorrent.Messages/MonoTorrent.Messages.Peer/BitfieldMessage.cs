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

namespace MonoTorrent.Messages.Peer
{
    /// <summary>
    /// 
    /// </summary>
    public class BitfieldMessage : PeerMessage
    {
        internal const byte MessageId = 5;

        public static readonly BitfieldMessage UnknownLength = new BitfieldMessage (new ReadOnlyBitField (1));

        /// <summary>
        /// The bitfield
        /// </summary>
        public ReadOnlyBitField BitField { get; }
        BitField? MutableBitField { get; }

        bool CanDecode { get; }

        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="length">The length of the bitfield</param>
        public BitfieldMessage (int length)
        {
            BitField = MutableBitField = new BitField (length);
            CanDecode = true;
        }

        /// <summary>
        /// Creates a new BitfieldMessage
        /// </summary>
        /// <param name="bitfield">The bitfield to use</param>
        public BitfieldMessage (ReadOnlyBitField bitfield)
        {
            BitField = bitfield;
            CanDecode = false;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            if (CanDecode && !(MutableBitField is null))
                MutableBitField.From (buffer);
        }

        public override int Encode (Span<byte> buffer)
        {
            if (BitField == null)
                throw new InvalidOperationException ("Cannot send a BitfieldMessage without a Bitfield. Are you trying to send a bitfield during metadata mode?");
            int written = buffer.Length;

            Write (ref buffer, BitField.LengthInBytes + 1);
            Write (ref buffer, MessageId);
            BitField.ToBytes (buffer);
            buffer = buffer.Slice (BitField.LengthInBytes);

            return written - buffer.Length;
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => ((BitField == null ? 0 : BitField.LengthInBytes) + 5);


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            return "BitfieldMessage";
        }

        public override bool Equals (object? obj)
        {
            if (!(obj is BitfieldMessage bf))
                return false;

            return BitField == null ? bf.BitField == null : BitField.SequenceEqual (bf.BitField);
        }

        public override int GetHashCode ()
            => BitField == null ? 0 : BitField.GetHashCode ();

        #endregion
    }
}
