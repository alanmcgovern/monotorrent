using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.FastPeer
{
    public class RejectRequestMessage : PeerMessage, IFastPeerMessage
    {
        internal static readonly byte MessageId = 0x10;
        public readonly int messageLength = 13;

        #region Member Variables

        /// <summary>
        ///     The offset in bytes of the block of data
        /// </summary>
        public int StartOffset { get; private set; }

        /// <summary>
        ///     The index of the piece
        /// </summary>
        public int PieceIndex { get; private set; }

        /// <summary>
        ///     The length of the block of data
        /// </summary>
        public int RequestLength { get; private set; }

        #endregion

        #region Constructors

        public RejectRequestMessage()
        {
        }


        public RejectRequestMessage(PieceMessage message)
            : this(message.PieceIndex, message.StartOffset, message.RequestLength)
        {
        }

        public RejectRequestMessage(RequestMessage message)
            : this(message.PieceIndex, message.StartOffset, message.RequestLength)
        {
        }

        public RejectRequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
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
            written += Write(buffer, written, StartOffset);
            written += Write(buffer, written, RequestLength);

            return CheckWritten(written - offset);
        }


        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");

            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = ReadInt(buffer, ref offset);
        }

        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        #endregion

        #region Overidden Methods

        public override bool Equals(object obj)
        {
            var msg = obj as RejectRequestMessage;
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


        public override string ToString()
        {
            var sb = new StringBuilder(24);
            sb.Append("Reject Request");
            sb.Append(" Index: ");
            sb.Append(PieceIndex);
            sb.Append(" Offset: ");
            sb.Append(StartOffset);
            sb.Append(" Length ");
            sb.Append(RequestLength);
            return sb.ToString();
        }

        #endregion
    }
}