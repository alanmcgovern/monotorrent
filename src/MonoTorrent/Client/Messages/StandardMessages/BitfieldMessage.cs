using MonoTorrent.Common;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// </summary>
    public class BitfieldMessage : PeerMessage
    {
        internal static readonly byte MessageId = 5;

        #region Member Variables

        /// <summary>
        ///     The bitfield
        /// </summary>
        public BitField BitField { get; }

        #endregion

        #region Constructors

        /// <summary>
        ///     Creates a new BitfieldMessage
        /// </summary>
        /// <param name="length">The length of the bitfield</param>
        public BitfieldMessage(int length)
        {
            BitField = new BitField(length);
        }


        /// <summary>
        ///     Creates a new BitfieldMessage
        /// </summary>
        /// <param name="bitfield">The bitfield to use</param>
        public BitfieldMessage(BitField bitfield)
        {
            BitField = bitfield;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BitField.FromArray(buffer, offset, length);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, BitField.LengthInBytes + 1);
            written += Write(buffer, written, MessageId);
            BitField.ToByteArray(buffer, written);
            written += BitField.LengthInBytes;

            return CheckWritten(written - offset);
        }

        /// <summary>
        ///     Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength
        {
            get { return BitField.LengthInBytes + 5; }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "BitfieldMessage";
        }

        public override bool Equals(object obj)
        {
            var bf = obj as BitfieldMessage;
            if (bf == null)
                return false;

            return BitField.Equals(bf.BitField);
        }

        public override int GetHashCode()
        {
            return BitField.GetHashCode();
        }

        #endregion
    }
}