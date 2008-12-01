//
// Message.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//



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
                throw new MessageException("Message encoded incorrectly. Incorrect number of bytes written");
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

        static public byte[] ReadBytes(byte[] buffer, int offset, int count)
        {
            return ReadBytes(buffer, ref offset, count);
        }

        static public byte[] ReadBytes(byte[] buffer, ref int offset, int count)
        {
            byte[] result = new byte[count];
            Buffer.BlockCopy(buffer, offset, result, 0, count);
            offset += count;
            return result;
        }

        static public short ReadShort(byte[] buffer, int offset)
        {
            return ReadShort(buffer, ref offset);
        }

        static public short ReadShort(byte[] buffer, ref int offset)
        {
            short ret = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, offset));
            offset += 2;
            return ret;
        }

        static public string ReadString(byte[] buffer, int offset, int count)
        {
            return ReadString(buffer, ref offset, count);
        }

        static public string ReadString(byte[] buffer, ref int offset, int count)
        {
            string s = System.Text.Encoding.ASCII.GetString(buffer, offset, count);
            offset += count;
            return s;
        }

        static public int ReadInt(byte[] buffer, int offset)
        {
            return ReadInt(buffer, ref offset);
        }

        static public int ReadInt(byte[] buffer, ref int offset)
        {
            int ret = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));
            offset += 4;
            return ret;
        }

        static public long ReadLong(byte[] buffer, int offset)
        {
            return ReadLong(buffer, ref offset);
        }

        static public long ReadLong(byte[] buffer, ref int offset)
        {
            long ret = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, offset));
            offset += 8;
            return ret;
        }

        static public int Write(byte[] buffer, int offset, byte value)
        {
            buffer[offset] = value;
            return 1;
        }

        static public int Write(byte[] dest, int destOffset, byte[] src, int srcOffset, int count)
        {
            Buffer.BlockCopy(src, srcOffset, dest, destOffset, count);
            return count;
        }

        static public int Write(byte[] buffer, int offset, ushort value)
        {
            return Write(buffer, offset, (short)value);
        }

        static public int Write(byte[] buffer, int offset, short value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static public int Write(byte[] buffer, int offset, int value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static public int Write(byte[] buffer, int offset, uint value)
        {
            return Write(buffer, offset, (int)value);
        }

        static public int Write(byte[] buffer, int offset, long value)
        {
            return Write(buffer, offset, BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)));
        }

        static public int Write(byte[] buffer, int offset, ulong value)
        {
            return Write(buffer, offset, (long)value);
        }

        static public int Write(byte[] buffer, int offset, byte[] value)
        {
            return Write(buffer, offset, value, 0, value.Length);
        }

        static public int WriteAscii(byte[] buffer, int offset, string text)
        {
            return Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, offset);
        }
    }
}