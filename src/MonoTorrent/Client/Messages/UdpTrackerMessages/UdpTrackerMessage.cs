namespace MonoTorrent.Client.Messages.UdpTracker
{
    public abstract class UdpTrackerMessage : Message
    {
        public UdpTrackerMessage(int action, int transactionId)
        {
            Action = action;
            TransactionId = transactionId;
        }

        public int Action { get; }

        public int TransactionId { get; protected set; }

        public static UdpTrackerMessage DecodeMessage(byte[] buffer, int offset, int count, MessageType type)
        {
            UdpTrackerMessage m = null;
            var action = type == MessageType.Request ? ReadInt(buffer, offset + 8) : ReadInt(buffer, offset);
            switch (action)
            {
                case 0:
                    if (type == MessageType.Request)
                        m = new ConnectMessage();
                    else
                        m = new ConnectResponseMessage();
                    break;
                case 1:
                    if (type == MessageType.Request)
                        m = new AnnounceMessage();
                    else
                        m = new AnnounceResponseMessage();
                    break;
                case 2:
                    if (type == MessageType.Request)
                        m = new ScrapeMessage();
                    else
                        m = new ScrapeResponseMessage();
                    break;
                case 3:
                    m = new ErrorMessage();
                    break;
                default:
                    throw new ProtocolException(string.Format("Invalid udp message received: {0}", buffer[offset]));
            }

            try
            {
                m.Decode(buffer, offset, count);
            }
            catch
            {
                m = new ErrorMessage(0, "Couldn't decode the tracker response");
            }
            return m;
        }

        protected static void ThrowInvalidActionException()
        {
            throw new MessageException("Invalid value for 'Action'");
        }
    }
}