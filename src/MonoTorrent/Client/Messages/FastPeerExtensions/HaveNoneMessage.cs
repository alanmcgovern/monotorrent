namespace MonoTorrent.Client.Messages.FastPeer
{
    public class HaveNoneMessage : PeerMessage, IFastPeerMessage
    {
        internal static readonly byte MessageId = 0x0F;
        private readonly int messageLength = 1;

        #region Constructors

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message encoding not supported");

            var written = offset;

            written += Write(buffer, written, messageLength);
            written += Write(buffer, written, MessageId);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");
        }

        public override int ByteLength
        {
            get { return messageLength + 4; }
        }

        #endregion

        #region Overidden Methods

        public override bool Equals(object obj)
        {
            return obj is HaveNoneMessage;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return "HaveNoneMessage";
        }

        #endregion
    }
}