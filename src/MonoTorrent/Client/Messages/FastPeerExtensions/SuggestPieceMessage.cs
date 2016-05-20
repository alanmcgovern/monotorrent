using System.Text;

namespace MonoTorrent.Client.Messages.FastPeer
{
    // FIXME: The only use for a SuggestPiece message is for when i load a piece into a Disk Cache and want to make use for it
    public class SuggestPieceMessage : PeerMessage, IFastPeerMessage
    {
        internal static readonly byte MessageId = 0x0D;
        private readonly int messageLength = 5;

        #region Member Variables

        /// <summary>
        ///     The index of the suggested piece to request
        /// </summary>
        public int PieceIndex { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///     Creates a new SuggestPiece message
        /// </summary>
        public SuggestPieceMessage()
        {
        }


        /// <summary>
        ///     Creates a new SuggestPiece message
        /// </summary>
        /// <param name="pieceIndex">The suggested piece to download</param>
        public SuggestPieceMessage(int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");

            var written = offset;

            written += Write(buffer, written, messageLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");

            PieceIndex = ReadInt(buffer, ref offset);
        }

        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        #endregion

        #region Overidden Methods

        public override bool Equals(object obj)
        {
            var msg = obj as SuggestPieceMessage;
            if (msg == null)
                return false;

            return PieceIndex == msg.PieceIndex;
        }

        public override int GetHashCode()
        {
            return PieceIndex.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder(24);
            sb.Append("Suggest Piece");
            sb.Append(" Index: ");
            sb.Append(PieceIndex);
            return sb.ToString();
        }

        #endregion
    }
}