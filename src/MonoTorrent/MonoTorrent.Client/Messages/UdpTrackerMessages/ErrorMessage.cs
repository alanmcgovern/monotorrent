using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ErrorMessage : UdpTrackerMessage
    {
        int action;
        int transactionId;
        string errorMessage;

        public ErrorMessage()
        {
        }

        public ErrorMessage(string error)
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
            action = ReadInt(buffer, ref offset);
            transactionId = ReadInt(buffer, ref offset);
            errorMessage = ReadString(buffer, ref offset, length - offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, action);
            written += Write(buffer, written, transactionId);
            written += WriteAscii(buffer, written, errorMessage);

            return written - offset; ;
        }
    }
}
