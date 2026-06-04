using System;
using System.Buffers.Binary;

namespace MonoTorrent.Messages
{
    public static class MessageDispatcher
    {
        public static ReadOnlyMemory<byte> NextMessage (ReadOnlyMemory<byte> buffer)
            => buffer.Slice (4 + BinaryPrimitives.ReadInt32BigEndian (buffer.Span));

        public static ReadOnlySpan<byte> NextMessage (ReadOnlySpan<byte> buffer)
            => buffer.Slice (4 + BinaryPrimitives.ReadInt32BigEndian (buffer));

        public static Memory<byte> NextMessage (Memory<byte> buffer)
            => buffer.Slice (4 + BinaryPrimitives.ReadInt32BigEndian (buffer.Span));

        public static Span<byte> NextMessage (Span<byte> buffer)
            => buffer.Slice (4 + BinaryPrimitives.ReadInt32BigEndian (buffer));

        public static MessageType GetType (ReadOnlyMemory<byte> buffer)
            => GetType (buffer.Span);

        public static MessageType GetType (ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 5)
                throw new ArgumentException ("Invalid buffer");

            return (MessageType) buffer[4];
        }

        public static ExtendedMessageType GetExtendedMessageType (ReadOnlyMemory<byte> buffer)
           => GetExtendedMessageType (buffer.Span);

        public static ExtendedMessageType GetExtendedMessageType (ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 6)
                throw new ArgumentException ("Invalid buffer");

            if (GetType (buffer) != MessageType.Extended)
                throw new InvalidOperationException ("This is not an extended message");

            return (ExtendedMessageType) buffer[5];
        }
    }
}
