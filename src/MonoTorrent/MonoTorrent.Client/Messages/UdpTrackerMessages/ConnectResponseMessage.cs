using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages;
namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ConnectResponseMessage : UdpTrackerMessage
    {
        int action = 0;
        long connectionId;
        int transactionId;

        public int Action
        {
            get { return action; }
        }

        public long ConnectionId
        {
            get { return connectionId; }
        }
        public int TransactionId
        {
            get { return transactionId; }
        }

        public ConnectResponseMessage()
        {

        }

        public ConnectResponseMessage(long connectionId, int transactionId)
        {
            this.connectionId = connectionId;
            this.transactionId = transactionId;
        }

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, ref offset);
            transactionId = ReadInt(buffer, ref offset);
            connectionId = ReadLong(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            Write(buffer, offset, Action);
            Write(buffer, offset + 4, TransactionId);
            Write(buffer, offset + 8, ConnectionId);
            
            return ByteLength;
        }
    }
}
