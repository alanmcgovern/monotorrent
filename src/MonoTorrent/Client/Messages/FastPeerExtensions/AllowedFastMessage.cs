using System.Text;

namespace MonoTorrent.Client.Messages.FastPeer
{
    public class AllowedFastMessage : PeerMessage, IFastPeerMessage
    {
        internal static readonly byte MessageId = 0x11;
        private readonly int messageLength = 5;

        #region Member Variables

        public int PieceIndex { get; private set; }

        #endregion

        #region Constructors

        internal AllowedFastMessage()
        {
        }

        internal AllowedFastMessage(int pieceIndex)
        {
            PieceIndex = pieceIndex;
        }

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message encoding not supported");

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

            PieceIndex = ReadInt(buffer, offset);
        }

        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        #endregion

        #region Overidden Methods

        public override bool Equals(object obj)
        {
            var msg = obj as AllowedFastMessage;
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
            sb.Append("AllowedFast");
            sb.Append(" Index: ");
            sb.Append(PieceIndex);
            return sb.ToString();
        }

        #endregion
    }
}