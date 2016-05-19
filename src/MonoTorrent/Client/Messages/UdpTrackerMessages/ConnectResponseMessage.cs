namespace MonoTorrent.Client.Messages.UdpTracker
{
    internal class ConnectResponseMessage : UdpTrackerMessage
    {
        public ConnectResponseMessage()
            : this(0, 0)
        {
        }

        public ConnectResponseMessage(int transactionId, long connectionId)
            : base(0, transactionId)
        {
            ConnectionId = connectionId;
        }

        public long ConnectionId { get; private set; }

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            ConnectionId = ReadLong(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, ConnectionId);

            return ByteLength;
        }
    }
}