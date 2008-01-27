using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages;
namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    class ConnectResponseMessage : Message
    {
        int action;
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

        public override int ByteLength
        {
            get { return 8 + 4 + 4; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            action = ReadInt(buffer, offset);
            transactionId = ReadInt(buffer, offset + 4);
            connectionId = ReadLong(buffer, offset + 8);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            Write(buffer, offset, action);
            Write(buffer, offset + 4, transactionId);
            Write(buffer, offset + 8, connectionId);
            
            return ByteLength;
        }
    }
}
