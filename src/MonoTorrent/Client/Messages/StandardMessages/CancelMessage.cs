using System.Text;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// </summary>
    public class CancelMessage : PeerMessage
    {
        private const int messageLength = 13;
        internal static readonly byte MessageId = 8;

        #region Member Variables

        /// <summary>
        ///     The index of the piece
        /// </summary>
        public int PieceIndex { get; private set; }


        /// <summary>
        ///     The offset in bytes of the block of data
        /// </summary>
        public int StartOffset { get; private set; }


        /// <summary>
        ///     The length in bytes of the block of data
        /// </summary>
        public int RequestLength { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///     Creates a new CancelMessage
        /// </summary>
        public CancelMessage()
        {
        }


        /// <summary>
        ///     Creates a new CancelMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece to cancel</param>
        /// <param name="startOffset">The offset in bytes of the block of data to cancel</param>
        /// <param name="requestLength">The length in bytes of the block of data to cancel</param>
        public CancelMessage(int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
        }

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, messageLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);
            written += Write(buffer, written, StartOffset);
            written += Write(buffer, written, RequestLength);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = ReadInt(buffer, ref offset);
        }

        /// <summary>
        ///     Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("CancelMessage ");
            sb.Append(" Index ");
            sb.Append(PieceIndex);
            sb.Append(" Offset ");
            sb.Append(StartOffset);
            sb.Append(" Length ");
            sb.Append(RequestLength);
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var msg = obj as CancelMessage;

            if (msg == null)
                return false;

            return PieceIndex == msg.PieceIndex
                   && StartOffset == msg.StartOffset
                   && RequestLength == msg.RequestLength;
        }

        public override int GetHashCode()
        {
            return PieceIndex.GetHashCode()
                   ^ RequestLength.GetHashCode()
                   ^ StartOffset.GetHashCode();
        }

        #endregion
    }
}