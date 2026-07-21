using System;
using System.Buffers.Binary;
using System.IO;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages.Peer.Libtorrent;

using static MonoTorrent.Messages.Extended.MetadataMessage;

namespace MonoTorrent.Messages
{
    partial class MessageEncoder
    {
        public static class Extended
        {
            internal static readonly ExtensionSupports SupportedMessages;

            public static readonly ExtensionSupport HandshakeSupport = new ExtensionSupport ("LT_handshake", (byte) ExtendedMessageType.Handshake);
            public static readonly ExtensionSupport PeerExchangeSupport = new ExtensionSupport ("ut_pex", (byte) ExtendedMessageType.PeerExchange);
            public static readonly ExtensionSupport MetadataExchangeSupport = new ExtensionSupport ("ut_metadata", (byte) ExtendedMessageType.Metadata);
            public static readonly ExtensionSupport ChatSupport = new ExtensionSupport ("LT_chat", (byte) ExtendedMessageType.Chat);

            static Extended ()
            {
                SupportedMessages = new ExtensionSupports (new[] {
                    HandshakeSupport,
                    PeerExchangeSupport,
                    MetadataExchangeSupport,
                    ChatSupport
                });
            }


            static int WriteHeader (Span<byte> destination, byte extensionId, int payloadLength)
            {
                BinaryPrimitives.WriteInt32BigEndian (
                    destination,
                    payloadLength + 2);

                destination[4] = 20;
                destination[5] = extensionId;

                return 6;
            }

            public static (Memory<byte> msg, ByteBufferPool.Releaser releaser) WriteHandshake (ReadOnlyMemory<byte> clientVersion, bool isPrivate, int? metadataSize, int? listenPort)
            {
                var releaser = MemoryPool.Default.Rent (1024, out var buffer);
                buffer = buffer.Slice (0, WriteHandshake (buffer.Span, clientVersion, isPrivate, metadataSize, listenPort));
                return (buffer, releaser);
            }

            public static int WriteHandshake (Span<byte> destination, ReadOnlyMemory<byte> clientVersion, bool isPrivate, int? metadataSize, int? listenPort)
            {
                var payload = destination.Slice (6);

                var writer = new BEncodeWriter (payload);

                writer.BeginDict ();

                writer.WriteString ("m"u8);
                writer.BeginDict ();
                foreach (var support in SupportedMessages) {
                    if (support == PeerExchangeSupport && isPrivate)
                        continue;
                    writer.WriteString (support.NameUtf8);
                    writer.WriteLong (support.MessageId);
                }
                writer.End ();

                if (listenPort.HasValue) {
                    writer.WriteString ("p"u8);
                    writer.WriteLong (listenPort.Value);
                }

                writer.WriteString ("reqq"u8);
                writer.WriteLong (Constants.DefaultMaxPendingRequests);

                if (metadataSize.HasValue) {
                    writer.WriteString ("metadata_size"u8);
                    writer.WriteLong (metadataSize.Value);
                }

                writer.WriteString ("v"u8);
                writer.WriteString (clientVersion.Span);

                writer.End ();

                WriteHeader (
                    destination,
                    extensionId: (byte) ExtendedMessageType.Handshake, // As per spec - '0' is always the extended handshake
                    payloadLength: writer.Written);

                return writer.Written + 6;
            }

            public static (Memory<byte> message, ByteBufferPool.Releaser releaser) WritePeerExchange (ExtensionSupports remoteSupports, ReadOnlySpan<byte> added, ReadOnlySpan<byte> addedDotF, ReadOnlySpan<byte> dropped, ReadOnlySpan<byte> added6, ReadOnlySpan<byte> added6DotF, ReadOnlySpan<byte> dropped6)
            {
                var releaser = MemoryPool.Default.Rent (100 + added.Length + addedDotF.Length + dropped.Length + added6.Length + added6DotF.Length + dropped6.Length, out var buffer);
                buffer = buffer.Slice (0, WritePeerExchange (buffer.Span, remoteSupports, added, addedDotF, dropped, added6, added6DotF, dropped6));
                return (buffer, releaser);
            }

            public static int WritePeerExchange (Span<byte> dest, ExtensionSupports remoteSupports, ReadOnlySpan<byte> added, ReadOnlySpan<byte> addedDotF, ReadOnlySpan<byte> dropped, ReadOnlySpan<byte> added6, ReadOnlySpan<byte> added6DotF, ReadOnlySpan<byte> dropped6)
            {
                var payload = dest.Slice (6);

                var writer = new BEncodeWriter (payload);

                writer.BeginDict ();
                writer.WriteString ("added"u8);
                writer.WriteString (added);
                writer.WriteString ("added.f"u8);
                writer.WriteString (addedDotF);

                writer.WriteString ("added6"u8);
                writer.WriteString (added6);
                writer.WriteString ("added6.f"u8);
                writer.WriteString (added6DotF);

                writer.WriteString ("dropped"u8);
                writer.WriteString (dropped);
                writer.WriteString ("dropped6"u8);
                writer.WriteString (dropped6);
                writer.End ();

                WriteHeader (
                    dest,
                    extensionId: remoteSupports.MessageId (PeerExchangeSupport),// Fill in the remote peer's id for this message
                    payloadLength: writer.Written);

                return writer.Written + 6;
            }

            public static (Memory<byte> dest, ByteBufferPool.Releaser) WriteMetadata(ExtensionSupports remoteSupports, MetadataMessageType type, int piece, ReadOnlySpan<byte> metadata)
            {
                var size = 64 + (type == MetadataMessageType.Data ? Math.Min (MetadataBlockSize, metadata.Length) : 0);
                var releaser = MemoryPool.Default.Rent (size, out var buffer);
                buffer = buffer.Slice (0, WriteMetadata (buffer.Span, remoteSupports, type, piece, metadata));
                return (buffer, releaser);
            }

            public static int WriteMetadata (Span<byte> dest, ExtensionSupports remoteSupports, MetadataMessageType type, int piece, ReadOnlySpan<byte> metadata)
            {
                var payload = dest.Slice (6);

                var writer = new BEncodeWriter (payload);
                writer.BeginDict ();
                writer.WriteString ("msg_type"u8);
                writer.WriteLong ((int) type);

                writer.WriteString ("piece"u8);
                writer.WriteLong (piece);

                if (type == MetadataMessageType.Data) {
                    if (metadata.IsEmpty)
                        throw new InvalidDataException ("The metadata data message did not contain any data.");
                    writer.WriteString ("total_size"u8);
                    writer.WriteLong (metadata.Length);
                }
                writer.End ();

                if (type == MetadataMessageType.Data) {
                    var metadataWritten = Math.Min (metadata.Length - piece * MetadataBlockSize, MetadataBlockSize);
                    writer.WriteRaw (metadata.Slice (piece * MetadataBlockSize, metadataWritten));
                }

                WriteHeader (
                    dest,
                    extensionId: remoteSupports.MessageId (MetadataExchangeSupport),
                    payloadLength: writer.Written);
                return writer.Written + 6;
            }
        }
    }
}
