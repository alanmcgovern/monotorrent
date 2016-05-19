namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    ///     Represents a "KeepAlive" message
    /// </summary>
    public class KeepAliveMessage : PeerMessage
    {
        private const int messageLength = 0; // has no payload
        internal static readonly byte MessageId = 0;
        private static readonly byte[] payload = {0, 0, 0, 0};

        #region Constructors

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, payload);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            // No decoding needed
        }

        /// <summary>
        ///     Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength
        {
            get { return 4; }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "KeepAliveMessage";
        }

        public override bool Equals(object obj)
        {
            return obj is KeepAliveMessage;
        }


        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        #endregion
    }
}