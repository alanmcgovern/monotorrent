using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Client.Messages
{
    public abstract class Message : IMessage
    {
        public abstract int ByteLength { get; }

        protected virtual void CheckWritten(int written)
        {
            if (written != ByteLength)
                throw new MessageException("Message encoded incorrectly. Correct number of bytes not written");
        }

        public abstract void Decode(byte[] buffer, int offset, int length);

        public void Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            Decode(buffer.Array, buffer.Offset + offset, length);
        }

        public byte[] Encode()
        {
            byte[] buffer = new byte[ByteLength];
            Encode(buffer, 0);
            return buffer;
        }

        public abstract int Encode(byte[] buffer, int offset);

        public int Encode(ArraySegment<byte> buffer, int offset)
        {
            return Encode(buffer.Array, buffer.Offset + offset);
        }

        static protected short ReadShort(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, offset));
        }

        static protected int ReadInt(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
        }

        static protected long ReadLong(byte[] buffer, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, offset));
        }

        static protected int Write(byte[] buffer, int offset, byte value)
        {
            buffer[offset] = value;
            return 1;
        }

        static protected int Write(byte[] buffer, int offset, byte[] value, int valueOffset, int count)
        {
            Buffer.BlockCopy(value, valueOffset, buffer, offset, count);
            return count;
        }

        static protected int Write(byte[] buffer, int offset, ushort value)
        {
            return Write(buffer, offset, (short)value);
        }

        private static int Write(byte[] buffer, int offset, byte[] value)
        {
            return Write(buffer, offset, value, 0, value.Length);
        }
        static protected int Write(byte[] buffer, int offset, short value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static protected int Write(byte[] buffer, int offset, int value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static protected int Write(byte[] buffer, int offset, uint value)
        {
            return Write(buffer, offset, (int)value);
        }

        static protected int Write(byte[] buffer, int offset, long value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static protected int Write(byte[] buffer, int offset, ulong value)
        {
            return Write(buffer, offset, (long)value);
        }
    }
}