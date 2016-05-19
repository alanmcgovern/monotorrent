using System;
using System.Text;

namespace MonoTorrent.Client.Messages.Standard
{
    public class PieceMessage : PeerMessage
    {
        private const int messageLength = 9;
        internal static readonly byte MessageId = 7;

        #region Private Fields

        internal byte[] Data;

        #endregion

        #region Properties

        internal int BlockIndex
        {
            get { return StartOffset/Piece.BlockSize; }
        }

        public override int ByteLength
        {
            get { return messageLength + RequestLength + 4; }
        }

        internal int DataOffset { get; private set; }

        public int PieceIndex { get; private set; }

        public int StartOffset { get; private set; }

        public int RequestLength { get; private set; }

        #endregion

        #region Constructors

        public PieceMessage()
        {
            Data = BufferManager.EmptyBuffer;
        }

        public PieceMessage(int pieceIndex, int startOffset, int blockLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = blockLength;
            Data = BufferManager.EmptyBuffer;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = length - 8;

            DataOffset = offset;

            // This buffer will be freed after the PieceWriter has finished with it
            Data = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref Data, RequestLength);
            Buffer.BlockCopy(buffer, offset, Data, 0, RequestLength);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, messageLength + RequestLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);
            written += Write(buffer, written, StartOffset);
            written += Write(buffer, written, Data, 0, RequestLength);

            return CheckWritten(written - offset);
        }

        public override bool Equals(object obj)
        {
            var msg = obj as PieceMessage;
            return msg == null
                ? false
                : PieceIndex == msg.PieceIndex
                  && StartOffset == msg.StartOffset
                  && RequestLength == msg.RequestLength;
        }

        public override int GetHashCode()
        {
            return RequestLength.GetHashCode()
                   ^ DataOffset.GetHashCode()
                   ^ PieceIndex.GetHashCode()
                   ^ StartOffset.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("PieceMessage ");
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