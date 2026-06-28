//
// UtpPacket.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
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

namespace MonoTorrent.Connections.Peer.Utp
{
    readonly struct UtpPacket
    {
        public static int HeaderSize => 20;

        // Total size of '_raw' should not exceed MTU size. Roughly 12 udp packets
        // per 16kB piece request for most connections.
        readonly Memory<byte> _raw;

        public Span<byte> Span => _raw.Span;

        public PacketType Type {
            get => (PacketType) ((Span[0] >> 4) & 0x0F);
            set => Span[0] = (byte) ((((byte) value & 0x0F) << 4) | (Span[0] & 0x0F));
        }
        public byte Version {
            get => (byte) (Span[0] & 0x0F);
            set => Span[0] = (byte) ((Span[0] & 0xF0) | value & 0x0F);
        }
        public byte Extension {
            get => Span[1];
            set => Span[1] = value;
        }
        public ushort ConnectionId {
            get => BinaryPrimitives.ReadUInt16BigEndian (Span.Slice (2, 2));
            set => BinaryPrimitives.WriteUInt16BigEndian (Span.Slice (2, 2), value);
        }
        public uint Timestamp {
            get => BinaryPrimitives.ReadUInt32BigEndian (Span.Slice (4, 4));
            private set => BinaryPrimitives.WriteUInt32BigEndian (Span.Slice (4, 4), value);
        }
        public uint TimestampDiff {
            get => BinaryPrimitives.ReadUInt32BigEndian (Span.Slice (8, 4));
            set => BinaryPrimitives.WriteUInt32BigEndian (Span.Slice (8, 4), value);
        }
        public uint WindowSize {
            get => BinaryPrimitives.ReadUInt32BigEndian (Span.Slice (12, 4));
            set => BinaryPrimitives.WriteUInt32BigEndian (Span.Slice (12, 4), value);
        }
        public ushort SequenceNumber {
            get => BinaryPrimitives.ReadUInt16BigEndian (Span.Slice (16, 2));
            set => BinaryPrimitives.WriteUInt16BigEndian (Span.Slice (16, 2), value);
        }
        public ushort AckNumber {
            get => BinaryPrimitives.ReadUInt16BigEndian (Span.Slice (18, 2));
            set => BinaryPrimitives.WriteUInt16BigEndian (Span.Slice (18, 2), value);
        }

        public Span<byte> Payload => _raw.Slice (HeaderSize).Span;

        public UtpPacket (Memory<byte> packet)
            => _raw = packet;

        public void SetTimestamp ()
            => Timestamp = (uint) (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds () * 1_000);
    }
}
