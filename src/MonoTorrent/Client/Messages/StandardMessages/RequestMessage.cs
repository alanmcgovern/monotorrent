using System.Text;

namespace MonoTorrent.Client.Messages.Standard
{
    public class RequestMessage : PeerMessage
    {
        private const int messageLength = 13;

        internal const int MaxSize = 65536 + 64;
        internal const int MinSize = 4096;
        internal static readonly byte MessageId = 6;

        #region Private Fields

        #endregion

        #region Public Properties

        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        public int StartOffset { get; private set; }

        public int PieceIndex { get; private set; }

        public int RequestLength { get; private set; }

        #endregion

        #region Constructors

        public RequestMessage()
        {
        }

        public RequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = ReadInt(buffer, ref offset);
        }

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

        public override bool Equals(object obj)
        {
            var msg = obj as RequestMessage;
            return msg == null
                ? false
                : PieceIndex == msg.PieceIndex
                  && StartOffset == msg.StartOffset
                  && RequestLength == msg.RequestLength;
        }

        public override int GetHashCode()
        {
            return PieceIndex.GetHashCode() ^ RequestLength.GetHashCode() ^ StartOffset.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("RequestMessage ");
            sb.Append(" Index ");
            sb.Append(PieceIndex);
            sb.Append(" Offset ");
            sb.Append(StartOffset);
            sb.Append(" Length ");
            sb.Append(RequestLength);
            return sb.ToString();
        }

        #endregion
    }
}