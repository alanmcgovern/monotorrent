using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client.Messages
{
    public abstract class Message : IMessage
    {
        public abstract int ByteLength { get; }

        public abstract void Decode(byte[] buffer, int offset, int length);

        public void Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            Decode(buffer.Array, buffer.Offset + offset, length);
        }

        public abstract int Encode(byte[] buffer, int offset);

        public int Encode(ArraySegment<byte> buffer, int offset)
        {
            return Encode(buffer.Array, buffer.Offset + offset);
        }

        protected short ReadShort(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, offset));
        }

        protected int ReadInt(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }

        protected long ReadLong(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, offset));
        }

        protected int Write(byte[] buffer, int offset, byte value)
        {
            buffer[offset] = value;
            return 1;
        }

        protected int Write(byte[] buffer, int offset, short value)
        {
            byte[] temp = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(temp, 0, buffer, offset, temp.Length);
            return temp.Length;
        } 

        protected int Write(byte[] buffer, int offset, int value)
        {
            byte[] temp = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(temp, 0, buffer, offset, temp.Length);
            return temp.Length;
        }

        protected int Write(byte[] buffer, int offset, long value)
        {
            byte[] temp = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(temp, 0, buffer, offset, temp.Length);
            return temp.Length;
        }
    }
}