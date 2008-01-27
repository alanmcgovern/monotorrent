using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    public class ConnectMessage : Message
    {
        private long connectionId;
        private int action;
        private int transactionId;

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

        public ConnectMessage()
        {
            action = 0;                                 // Connect message
            connectionId = 0x41727101980;               // Init connectionId as per spec
            transactionId = DateTime.Now.GetHashCode(); // Random ID created from current datetime
        }

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, offset);
            action = ReadInt(buffer, offset + 8);
            transactionId = ReadInt(buffer, offset + 12);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int count = Write(buffer, offset, connectionId);
            count += Write(buffer, offset, action);
            count += Write(buffer, offset, transactionId);
            return count;
        }
    }
}
