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
using System.Runtime.CompilerServices;
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages
{
    public abstract class Message : IMessage
    {
        public abstract int ByteLength { get; }

        public abstract void Decode (ReadOnlySpan<byte> buffer);

        public ReadOnlyMemory<byte> Encode ()
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
            var value = BEncodedValue.Decode (buffer, strictDecoding);
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
        public static ReadOnlyMemory<byte> ReadBytes (ref ReadOnlySpan<byte> buffer, int length)
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
            string result;
#if NETSTANDARD2_0 || NET472
            using (MemoryPool.Default.Rent (length, out ArraySegment<byte> segment)) {
                buffer.Slice (0, length).CopyTo (segment.AsSpan ());
                result = Encoding.ASCII.GetString (segment.Array, segment.Offset, segment.Count);
            }
#else
            result = Encoding.ASCII.GetString (buffer.Slice (0, length));
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
#if NETSTANDARD2_0 || NET472
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
            int written = value.Encode (buffer);
            buffer = buffer.Slice (written);
        }
    }
}
