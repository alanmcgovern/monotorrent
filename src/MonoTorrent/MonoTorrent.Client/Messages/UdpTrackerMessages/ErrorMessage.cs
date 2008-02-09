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
            get { return 8 + Encoding.ASCII.GetByteCount(errorMessage); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, offset); offset += 4;
            transactionId = ReadInt(buffer, offset); offset += 4;
            errorMessage = Encoding.ASCII.GetString(buffer, offset, length - offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            offset += Write(buffer, offset, action);
            offset += Write(buffer, offset, transactionId);
            offset += Encoding.ASCII.GetBytes(errorMessage, 0, errorMessage.Length, buffer, offset);
            
            return ByteLength;
        }
    }
}
