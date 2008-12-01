using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ErrorMessage : UdpTrackerMessage
    {
        string errorMessage;

        public ErrorMessage()
            :this(0, "")
        {
        }

        public ErrorMessage(int transactionId, string error)
            :base(3, transactionId)
        {
            this.errorMessage = error;
        }

        public string Error
        {
            get { return errorMessage; }
        }

        public override int ByteLength
        {
            get { return 4 + 4 + Encoding.ASCII.GetByteCount(errorMessage); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            errorMessage = ReadString(buffer, ref offset, length - offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += WriteAscii(buffer, written, errorMessage);

            return written - offset; ;
        }
    }
}
