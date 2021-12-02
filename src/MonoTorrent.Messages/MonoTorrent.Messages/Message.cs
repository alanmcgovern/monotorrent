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
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages
{
    public abstract class Message : IMessage
    {
        public abstract int ByteLength { get; }

        public abstract void Decode (ReadOnlySpan<byte> buffer);

        public byte[] Encode ()
        {
            byte[] buffer = new byte[ByteLength];
            Encode (buffer);
            return buffer;
        }

        public abstract int Encode (Span<byte> buffer);

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected T ReadBencodedValue<T> (ref ReadOnlySpan<byte> buffer, bool strictDecoding)
            where T : BEncodedValue
        {
            var value = BEncodedValue.Decode (buffer.ToArray (), strictDecoding);
            buffer = buffer.Slice (value.LengthInBytes ());
            return (T) value;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte (ref ReadOnlySpan<byte> buffer)
        {
            var result = buffer[0];
            buffer = buffer.Slice (1);
            return result;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static byte[] ReadBytes (ref ReadOnlySpan<byte> buffer, int length)
        {
            var result = buffer.Slice (0, length).ToArray ();
            buffer = buffer.Slice (length);
            return result;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static short ReadShort (ReadOnlySpan<byte> buffer)
        {
            return BinaryPrimitives.ReadInt16BigEndian (buffer);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static short ReadShort (ref ReadOnlySpan<byte> buffer)
        {
            var ret = BinaryPrimitives.ReadInt16BigEndian (buffer);
            buffer = buffer.Slice (2);
            return ret;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static long ReadLong (ref ReadOnlySpan<byte> buffer)
        {
            var ret = BinaryPrimitives.ReadInt64BigEndian (buffer);
            buffer = buffer.Slice (8);
            return ret;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt (ref ReadOnlySpan<byte> buffer)
        {
            var ret = BinaryPrimitives.ReadUInt32BigEndian (buffer);
            buffer = buffer.Slice (4);
            return ret;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUShort (ref ReadOnlySpan<byte> buffer)
        {
            var ret = BinaryPrimitives.ReadUInt16BigEndian (buffer);
            buffer = buffer.Slice (2);
            return ret;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static string ReadString (ref ReadOnlySpan<byte> buffer, int length)
        {
#if NETSTANDARD2_0
            var array = buffer.Slice (0, length).ToArray ();
            var result = Encoding.ASCII.GetString (array);
#else
            var result = Encoding.ASCII.GetString (buffer.Slice (0, length));
#endif
            buffer = buffer.Slice (length);
            return result;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int ReadInt (ReadOnlySpan<byte> buffer)
        {
            return BinaryPrimitives.ReadInt32BigEndian (buffer);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static int ReadInt (ref ReadOnlySpan<byte> buffer)
        {
            var val = BinaryPrimitives.ReadInt32BigEndian (buffer);
            buffer = buffer.Slice (4);
            return val;
        }


        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, InfoHash infoHash)
        {
            infoHash.Span.CopyTo (buffer);
            buffer = buffer.Slice (infoHash.Span.Length);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Memory<byte> buffer, ReadOnlySpan<byte> data)
        {
            data.CopyTo (buffer.Span);
            buffer = buffer.Slice (data.Length);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, ReadOnlySpan<byte> data)
        {
            data.CopyTo (buffer);
            buffer = buffer.Slice (data.Length);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (Span<byte> buffer, byte value)
        {
            buffer[0] = value;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Memory<byte> buffer, byte value)
        {
            buffer.Span[0] = value;
            buffer = buffer.Slice (1);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, byte value)
        {
            buffer[0] = value;
            buffer = buffer.Slice (1);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Memory<byte> buffer, short value)
        {
            BinaryPrimitives.WriteInt16BigEndian (buffer.Span, value);
            buffer = buffer.Slice (2);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void WriteAscii (ref Span<byte> buffer, string value)
        {
#if NETSTANDARD2_0
            for (int i = 0; i < value.Length; i++)
                buffer[i] = (byte) value[i];
            buffer = buffer.Slice (value.Length);
#else
            buffer = buffer.Slice (Encoding.ASCII.GetBytes (value, buffer));
#endif
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, ushort value)
        {
            BinaryPrimitives.WriteUInt16BigEndian (buffer, value);
            buffer = buffer.Slice (2);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian (buffer, value);
            buffer = buffer.Slice (4);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Memory<byte> buffer, int value)
        {
            BinaryPrimitives.WriteInt32BigEndian (buffer.Span, value);
            buffer = buffer.Slice (4);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (Span<byte> buffer, int value)
        {
            BinaryPrimitives.WriteInt32BigEndian (buffer, value);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, int value)
        {
            BinaryPrimitives.WriteInt32BigEndian (buffer, value);
            buffer = buffer.Slice (4);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, long value)
        {
            BinaryPrimitives.WriteInt64BigEndian (buffer, value);
            buffer = buffer.Slice (8);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static void Write (ref Span<byte> buffer, BEncodedValue value)
        {
            var array = value.Encode ();
            array.AsSpan ().CopyTo (buffer);
            buffer = buffer.Slice (array.Length);
        }


        public static byte ReadByte (byte[] buffer, ref int offset)
        {
            byte b = buffer[offset];
            offset++;
            return b;
        }

        public static byte[] ReadBytes (byte[] buffer, int offset, int count)
        {
            return ReadBytes (buffer, ref offset, count);
        }

        public static byte[] ReadBytes (byte[] buffer, ref int offset, int count)
        {
            byte[] result = new byte[count];
            Buffer.BlockCopy (buffer, offset, result, 0, count);
            offset += count;
            return result;
        }

        public static short ReadShort (byte[] buffer, int offset)
        {
            return ReadShort (buffer, ref offset);
        }

        public static short ReadShort (byte[] buffer, ref int offset)
        {
            short ret = IPAddress.NetworkToHostOrder (BitConverter.ToInt16 (buffer, offset));
            offset += 2;
            return ret;
        }

        public static string ReadString (byte[] buffer, int offset, int count)
        {
            return ReadString (buffer, ref offset, count);
        }

        public static string ReadString (byte[] buffer, ref int offset, int count)
        {
            string s = System.Text.Encoding.ASCII.GetString (buffer, offset, count);
            offset += count;
            return s;
        }

        public static int ReadInt (byte[] buffer, int offset)
        {
            return ReadInt (buffer, ref offset);
        }

        public static int ReadInt (byte[] buffer, ref int offset)
        {
            int ret = IPAddress.NetworkToHostOrder (BitConverter.ToInt32 (buffer, offset));
            offset += 4;
            return ret;
        }

        public static long ReadLong (byte[] buffer, int offset)
        {
            return ReadLong (buffer, ref offset);
        }

        public static long ReadLong (byte[] buffer, ref int offset)
        {
            long ret = IPAddress.NetworkToHostOrder (BitConverter.ToInt64 (buffer, offset));
            offset += 8;
            return ret;
        }

        public static int Write (byte[] buffer, int offset, byte value)
        {
            buffer[offset] = value;
            return 1;
        }

        public static int Write (byte[] dest, int destOffset, byte[] src, int srcOffset, int count)
        {
            Buffer.BlockCopy (src, srcOffset, dest, destOffset, count);
            return count;
        }

        public static int Write (byte[] buffer, int offset, ushort value)
        {
            buffer[offset + 0] = (byte) (value >> 8);
            buffer[offset + 1] = (byte) value;
            return 2;
        }

        public static int Write (byte[] buffer, int offset, short value)
        {
            buffer[offset + 0] = (byte) (value >> 8);
            buffer[offset + 1] = (byte) value;
            return 2;
        }
        public static int Write (byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte) (value >> 24);
            buffer[offset + 1] = (byte) (value >> 16);
            buffer[offset + 2] = (byte) (value >> 8);
            buffer[offset + 3] = (byte) value;
            return 4;
        }

        public static int Write (byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte) (value >> 24);
            buffer[offset + 1] = (byte) (value >> 16);
            buffer[offset + 2] = (byte) (value >> 8);
            buffer[offset + 3] = (byte) value;
            return 4;
        }

        public static int Write (byte[] buffer, int offset, long value)
        {
            buffer[offset + 0] = (byte) (value >> 56);
            buffer[offset + 1] = (byte) (value >> 48);
            buffer[offset + 2] = (byte) (value >> 40);
            buffer[offset + 3] = (byte) (value >> 32);
            buffer[offset + 4] = (byte) (value >> 24);
            buffer[offset + 5] = (byte) (value >> 16);
            buffer[offset + 6] = (byte) (value >> 8);
            buffer[offset + 7] = (byte) value;
            return 8;
        }

        public static int Write (byte[] buffer, int offset, ulong value)
        {
            buffer[offset + 0] = (byte) (value >> 56);
            buffer[offset + 1] = (byte) (value >> 48);
            buffer[offset + 2] = (byte) (value >> 40);
            buffer[offset + 3] = (byte) (value >> 32);
            buffer[offset + 4] = (byte) (value >> 24);
            buffer[offset + 5] = (byte) (value >> 16);
            buffer[offset + 6] = (byte) (value >> 8);
            buffer[offset + 7] = (byte) value;
            return 8;
        }

        public static int Write (byte[] buffer, int offset, byte[] value)
        {
            return Write (buffer, offset, value, 0, value.Length);
        }

        public static int WriteAscii (byte[] buffer, int offset, string text)
        {
            for (int i = 0; i < text.Length; i++)
                buffer[offset + i] = (byte) text[i];
            return text.Length;
        }
    }
}
