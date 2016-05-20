using System.Text;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    ///     Represents a "Have" message
    /// </summary>
    public class HaveMessage : PeerMessage
    {
        private const int messageLength = 5;
        internal static readonly byte MessageId = 4;

        #region Member Variables

        /// <summary>
        ///     The index of the piece that you "have"
        /// </summary>
        public int PieceIndex { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///     Creates a new HaveMessage
        /// </summary>
        public HaveMessage()
        {
        }


        /// <summary>
        ///     Creates a new HaveMessage
        /// </summary>
        /// <param name="pieceIndex">The index of the piece that you "have"</param>
        public HaveMessage(int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, messageLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, offset);
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
            sb.Append("HaveMessage ");
            sb.Append(" Index ");
            sb.Append(PieceIndex);
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var msg = obj as HaveMessage;

            if (msg == null)
                return false;

            return PieceIndex == msg.PieceIndex;
        }

        public override int GetHashCode()
        {
            return PieceIndex.GetHashCode();
        }

        #endregion
    }
}