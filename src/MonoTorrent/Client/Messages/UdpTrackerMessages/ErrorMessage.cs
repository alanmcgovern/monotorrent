using System.Text;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class ErrorMessage : UdpTrackerMessage
    {
        public ErrorMessage()
            : this(0, "")
        {
        }

        public ErrorMessage(int transactionId, string error)
            : base(3, transactionId)
        {
            Error = error;
        }

        public string Error { get; private set; }

        public override int ByteLength
        {
            get { return 4 + 4 + Encoding.ASCII.GetByteCount(Error); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            Error = ReadString(buffer, ref offset, length - offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += WriteAscii(buffer, written, Error);

            return written - offset;
            ;
        }
    }
}