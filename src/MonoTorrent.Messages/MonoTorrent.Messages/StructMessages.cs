using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages.Peer.Libtorrent;

using static MonoTorrent.Messages.Extended.MetadataMessage;

namespace MonoTorrent.Messages
{
    public readonly ref struct HandshakeMessage
    {
        public static readonly int HandshakeLength = 68;

        internal const byte ExtendedMessagingFlag = 0b00010000;
        internal const byte FastPeersFlag = 0b00000100;
        internal const byte UpgradeToV2Flag = 0b00010000;

        readonly ReadOnlyMemory<byte> _memory;

        public HandshakeMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public ReadOnlySpan<byte> InfoHash => _memory.Slice (28, 20).Span;

        public ReadOnlySpan<byte> PeerId => _memory.Slice (48, 20).Span;

        // The handshake can be encrypted, so return an empty buffer if that's the case
        // as the first 'length' byte won't be able to index into the message.
        public ReadOnlySpan<byte> ProtocolString => _memory.Span[0] == 19 ? _memory.Span.Slice (1, _memory.Span[0]) : default;

        public bool EnableExtended => (_memory.Span[20 + 5] & HandshakeMessage.ExtendedMessagingFlag) == HandshakeMessage.ExtendedMessagingFlag;
        public bool EnableFastPeer => (_memory.Span[20 + 7] & HandshakeMessage.FastPeersFlag) == HandshakeMessage.FastPeersFlag;
        public bool SupportsUpgradeToV2 => (_memory.Span[20 + 7] & HandshakeMessage.UpgradeToV2Flag) == HandshakeMessage.UpgradeToV2Flag;
    }

    public readonly ref struct KeepAliveMessage
    {
    }

    public readonly ref struct ChokeMessage
    {
    }
    public readonly ref struct UnchokeMessage
    {
    }

    public readonly ref struct InterestedMessage
    {
        static readonly ReadOnlyMemory<byte> instance;

        public static ReadOnlySpan<byte> Instance => instance.Span;
        static InterestedMessage ()
        {
            var b = new byte[5];
            BtEncoder.WriteInterested (b);
            instance = b;
        }
    }

    public readonly ref struct NotInterestedMessage
    {
        static readonly ReadOnlyMemory<byte> instance;
        public static ReadOnlySpan<byte> Instance => instance.Span;

        static NotInterestedMessage ()
        {
            var b = new byte[5];
            BtEncoder.WriteNotInterested (b);
            instance = b;
        }
    }

    public readonly ref struct HaveMessage
    {
        public static readonly int EncodedLength = 9;

        private readonly ReadOnlyMemory<byte> _memory;
        public HaveMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct BitfieldMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public BitfieldMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public ReadOnlySpan<byte> BitField =>
            _memory.Span.Slice (5);
    }

    public readonly ref struct RequestMessage
    {
        public static readonly int EncodedLength = 17;

        private readonly ReadOnlySpan<byte> _span;
        public RequestMessage (ReadOnlyMemory<byte> memory) => _span = memory.Span;
        public RequestMessage (ReadOnlySpan<byte> span) => _span = span;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_span.Slice (5));

        public int StartOffset =>
            BinaryPrimitives.ReadInt32BigEndian (_span.Slice (9));

        public int RequestLength =>
            BinaryPrimitives.ReadInt32BigEndian (_span.Slice (13));
    }

    public readonly ref struct PieceMessage
    {
        public static int EncodedLength (int requestLength)
            => 13 + requestLength;

        private readonly ReadOnlyMemory<byte> _memory;

        public PieceMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (5));

        public int StartOffset =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (9));

        public ReadOnlySpan<byte> Data
            => Span.Slice (DataOffset);

        public int DataOffset
            => EncodedLength (0);

        public int RequestLength =>
            Data.Length;
    }


    public readonly ref struct CancelMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public CancelMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (5));

        public int StartOffset =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (9));

        public int RequestLength =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (13));
    }

    public readonly ref struct SuggestMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public SuggestMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct RejectRequestMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;

        public RejectRequestMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (5));

        public int StartOffset =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (9));

        public int RequestLength =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (13));
    }

    public readonly ref struct AllowedFastMessage
    {
        public static readonly int EncodedLength = 9;

        private readonly ReadOnlyMemory<byte> _memory;

        public AllowedFastMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;


        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct HaveAllMessage
    {
        static readonly ReadOnlyMemory<byte> instance;
        public static ReadOnlySpan<byte> Instance => instance.Span;
        static HaveAllMessage ()
        {
            var b = new byte[5];
            BtEncoder.WriteHaveAll (b);
            instance = b;
        }
    }

    public readonly ref struct HaveNoneMessage
    {
        public static ReadOnlyMemory<byte> instance;
        public static ReadOnlySpan<byte> Instance => instance.Span;
        static HaveNoneMessage ()
        {
            var b = new byte[5];
            BtEncoder.WriteHaveNone (b);
            instance = b;
        }
    }

    public readonly ref struct PortMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public PortMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public ushort Port =>
            BinaryPrimitives.ReadUInt16BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct HashRequestMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public HashRequestMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public ReadOnlySpan<byte> PiecesRoot
            => Span.Slice (5, 32);

        public int BaseLayer =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (37));

        public int Index =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (41));

        public int Length =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (45));

        public int ProofLayers =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (49));
    }

    public readonly ref struct HashesMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public HashesMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public ReadOnlySpan<byte> PiecesRoot
            => Span.Slice (5, 32);

        public int BaseLayer =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (37));

        public int Index =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (41));

        public int Length =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (45));

        public int ProofLayers =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (49));

        public ReadOnlySpan<byte> Hashes =>
            Span.Slice (53);
    }

    public readonly ref struct HashRejectMessage
    {
        private readonly ReadOnlyMemory<byte> _memory;
        public HashRejectMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        private ReadOnlySpan<byte> Span => _memory.Span;

        public ReadOnlySpan<byte> PiecesRoot
            => Span.Slice (5, 32);

        public int BaseLayer =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (37));

        public int Index =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (41));

        public int Length =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (45));

        public int ProofLayers =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (49));
    }


    /// <summary>
    /// LibTorrent extension protocol
    /// </summary>
    public static class Extended
    {
        public readonly ref struct HandshakeMessage
        {
            readonly ReadOnlyMemory<byte> _memory;

            public HandshakeMessage (ReadOnlyMemory<byte> memory)
            {
                _memory = memory;

                //
                var payload = memory.Slice (6);

                var reader = new BEncodeReader (payload.Span);
                reader.ExpectDictionaryBegin ();

                Mappings = default;
                ClientName = default;
                ListenPort = default;
                RequestQueue = default;
                MetadataSize = default;

                while (reader.TryReadKey (out var key)) {
                    if (key.SequenceEqual ("m"u8))
                        Mappings = reader.CaptureAny (payload);

                    else if (key.SequenceEqual ("v"u8))
                        ClientName = reader.CaptureString (payload);

                    else if (key.SequenceEqual ("p"u8))
                        ListenPort = reader.CaptureInteger (payload);

                    else if (key.SequenceEqual ("reqq"u8))
                        RequestQueue = reader.CaptureInteger (payload);

                    else if (key.SequenceEqual ("metadata_size"u8))
                        MetadataSize = reader.CaptureInteger (payload);

                    else
                        reader.SkipValue ();
                }
            }

            public ReadOnlyMemory<byte> Mappings { get; }

            ReadOnlyMemory<byte> ClientName { get; }

            ReadOnlyMemory<byte> ListenPort { get; }

            ReadOnlyMemory<byte> RequestQueue { get; }

            ReadOnlyMemory<byte> MetadataSize { get; }

            public string? ClientNameString {
                get {
                    BEncodeReader reader = new BEncodeReader (ListenPort.Span);
                    return reader.Read () && reader.Token == BEncodeToken.String ? Encoding.UTF8.GetString (reader.Span) : null;
                }
            }

            public int? Port {
                get {
                    BEncodeReader reader = new BEncodeReader (ListenPort.Span);
                    return reader.Read () && reader.Token == BEncodeToken.Integer ? (int) reader.Integer : null;
                }
            }

            public int? MaxRequests {
                get {
                    BEncodeReader reader = new BEncodeReader (RequestQueue.Span);
                    return reader.Read () && reader.Token == BEncodeToken.Integer ? (int) reader.Integer : null;
                }
            }

            public int? MetadataBytes {
                get {
                    BEncodeReader reader = new BEncodeReader (MetadataSize.Span);
                    return reader.Read () && reader.Token == BEncodeToken.Integer ? (int) reader.Integer : null;
                }
            }
        }

        public readonly ref struct MetadataMessage
        {
            public const int MetadataBlockSize = 16384;

            public enum MetadataMessageType
            {
                Request = 0,
                Data = 1,
                Reject = 2
            }

            readonly ReadOnlyMemory<byte> _memory;

            public MetadataMessage (ReadOnlyMemory<byte> memory)
            {
                _memory = memory;

                var payload = memory.Slice (6);

                var reader = new BEncodeReader (payload.Span);
                reader.ExpectDictionaryBegin ();

                while (reader.TryReadKey (out var key)) {
                    if (key.SequenceEqual ("msg_type"u8)) {
                        reader.CaptureInteger (memory);
                        MessageType = (MetadataMessageType) reader.Integer;
                    } else if (key.SequenceEqual ("piece"u8)) {
                        reader.CaptureInteger (memory);
                        Piece = (int) reader.Integer;
                    } else if (key.SequenceEqual ("total_size"u8)) {
                        reader.CaptureInteger (memory);
                        TotalSize = (int) reader.Integer;
                    } else
                        reader.SkipValue ();
                }

                MetadataData = _memory.Slice (6 + reader.Position);
            }

            public MetadataMessageType MessageType { get; }

            public readonly int Piece;

            public readonly int? TotalSize;

            public readonly ReadOnlyMemory<byte> MetadataData;
        }

        public readonly ref struct PeerExchangeMessage
        {
            readonly ReadOnlyMemory<byte> _memory;

            public PeerExchangeMessage (ReadOnlyMemory<byte> memory)
            {
                _memory = memory;

                var payload = memory.Slice (6);
                var reader = new BEncodeReader (payload.Span);
                reader.ExpectDictionaryBegin ();

                while (reader.TryReadKey (out var key)) {
                    if (key.SequenceEqual ("added"u8)) {
                        Added = reader.CaptureString (payload).Span;
                    } else if (key.SequenceEqual ("added.f"u8)) {
                        AddedDotF = reader.CaptureString (payload).Span;
                    } else if (key.SequenceEqual ("dropped"u8)) {
                        Dropped = reader.CaptureString (payload).Span;
                    } else if (key.SequenceEqual ("added6"u8)) {
                        Added6 = reader.CaptureString (payload).Span;
                    } else if (key.SequenceEqual ("added6.f"u8)) {
                        Added6DotF = reader.CaptureString (payload).Span;
                    } else if (key.SequenceEqual ("dropped6"u8)) {
                        Dropped6 = reader.CaptureString (payload).Span;
                    } else
                        reader.SkipValue ();
                }
            }

            public readonly ReadOnlySpan<byte> Added;
            public readonly ReadOnlySpan<byte> AddedDotF;
            public readonly ReadOnlySpan<byte> Dropped;

            public readonly ReadOnlySpan<byte> Added6;
            public readonly ReadOnlySpan<byte> Added6DotF;
            public readonly ReadOnlySpan<byte> Dropped6;
        }
    }

    partial class BtEncoder
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

    public static partial class BtEncoder
    {
        static void WriteHeader (Span<byte> span, MessageType id, int payloadLength)
        {
            BinaryPrimitives.WriteInt32BigEndian (span, payloadLength + 1);
            span[4] = (byte) id;
        }

        public static (Memory<byte> message, ByteBufferPool.Releaser releaser) WriteHandshake (ReadOnlySpan<byte> infoHash, ReadOnlySpan<byte> peerId, bool enableFastPeer, bool enableExtended, bool supportUpgradeToV2)
        {
            var releaser = MemoryPool.Default.Rent (68, out var buffer);
            buffer = buffer.Slice (0, WriteHandshake (buffer.Span, infoHash, peerId, enableFastPeer, enableExtended, supportUpgradeToV2));
            return (buffer, releaser);
        }

        public static int WriteHandshake (Span<byte> buffer, ReadOnlySpan<byte> infoHash, ReadOnlySpan<byte> peerId, bool enableFastPeer, bool enableExtended, bool supportUpgradeToV2)
        {
            buffer[0] = (byte) Constants.ProtocolStringV100UTF8.Length;
            Constants.ProtocolStringV100UTF8.CopyTo (buffer.Slice (1, 19));

            Span<byte> supports = buffer.Slice (20, 8);
            supports.Clear ();

            if (enableExtended)
                supports[5] |= HandshakeMessage.ExtendedMessagingFlag;
            if (enableFastPeer)
                supports[7] |= HandshakeMessage.FastPeersFlag;
            if (supportUpgradeToV2)
                supports[7] |= HandshakeMessage.UpgradeToV2Flag;

            infoHash.CopyTo (buffer.Slice (28, 20));
            peerId.CopyTo (buffer.Slice (48, 20));

            return 68;
        }


        public static (Memory<byte>, ByteBufferPool.Releaser) WriteChoke ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteChoke (buffer.Span));
            return (buffer, releaser);
        }

        public static int WriteChoke (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.Choke, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteUnchoke ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteUnchoke (buffer.Span));
            return (buffer, releaser);
        }

        public static int WriteUnchoke (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.Unchoke, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteInterested ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteInterested (buffer.Span));
            return (buffer, releaser);
        }

        public static int WriteInterested (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.Interested, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteNotInterested ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteNotInterested (buffer.Span));
            return (buffer, releaser);
        }

        public static int WriteNotInterested (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.NotInterested, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser releaser) WriteHave (int index)
        {
            var releaser = MemoryPool.Default.Rent (9, out Memory<byte> buffer);
            buffer = buffer.Slice (0, WriteHave (buffer.Span, index));
            return (buffer, releaser);
        }

        public static int WriteHave (Span<byte> dest, int index)
        {
            WriteHeader (dest, MessageType.Have, 4);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), index);
            return 9;
        }

        public static (Memory<byte> msg, ByteBufferPool.Releaser releaser) WriteBitfield (ReadOnlyBitField bitfield)
        {
            var releaser = MemoryPool.Default.Rent (5 + bitfield.LengthInBytes, out var buffer);
            buffer = buffer.Slice (0, WriteBitfield (buffer.Span, bitfield));
            return (buffer, releaser);
        }

        public static int WriteBitfield (Span<byte> dest, ReadOnlyBitField bitfield)
        {
            WriteHeader (dest, MessageType.Bitfield, bitfield.LengthInBytes);
            bitfield.ToBytes (dest.Slice (5));
            return 5 + bitfield.LengthInBytes;
        }

        public static int WriteRequest (
            Span<byte> dest,
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            WriteHeader (dest, MessageType.Request, 12);

            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), pieceIndex);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (9), startOffset);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (13), requestLength);

            return 17;
        }

        /// <summary>
        /// Writes a piece message, excluding content. Space for content is reserved so it can be appended later.
        /// </summary>
        /// <param name="pieceIndex"></param>
        /// <param name="startOffset"></param>
        /// <param name="requestLength"></param>
        /// <returns></returns>
        public static (Memory<byte>, ByteBufferPool.Releaser releaser) WriteSparsePiece (
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            var releaser = MemoryPool.Default.Rent (5 + 8 + requestLength, out Memory<byte> dest);
            WriteHeader (dest.Span, MessageType.Piece, 8 + requestLength);

            BinaryPrimitives.WriteInt32BigEndian (dest.Span.Slice (5), pieceIndex);
            BinaryPrimitives.WriteInt32BigEndian (dest.Span.Slice (9), startOffset);

            return (dest, releaser);
        }

        internal static void AppendPieceData (Memory<byte> dest, ReadOnlySpan<byte> pieceData)
        {
            if (MessageDispatcher.GetType (dest) != MessageType.Piece)
                throw new InvalidOperationException ();

            pieceData.CopyTo (dest.Slice (13).Span);
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteCancel (
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            var releaser = MemoryPool.Default.Rent (5 + 12, out var buffer);
            buffer = buffer.Slice (0, WriteCancel (buffer.Span, pieceIndex, startOffset, requestLength));
            return (buffer, releaser);
        }

        public static int WriteCancel (
            Span<byte> dest,
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            WriteHeader (dest, MessageType.Cancel, 12);

            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), pieceIndex);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (9), startOffset);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (13), requestLength);

            return 17;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteKeepAlive ()
        {
            var releaser = MemoryPool.Default.Rent (4, out var buffer);
            BinaryPrimitives.WriteInt32BigEndian (buffer.Span, 0);
            return (buffer, releaser);
        }

        public static int WriteKeepAlive (Span<byte> dest)
        {
            BinaryPrimitives.WriteInt32BigEndian (dest, 0);
            return 4;
        }

        // DHT (bep5)
        public static int WritePort (Span<byte> dest, ushort port)
        {
            WriteHeader (dest, MessageType.Port, 2);
            BinaryPrimitives.WriteUInt16BigEndian (dest.Slice (5), port);
            return 7;
        }

        // fast extensions
        public static int WriteSuggest (Span<byte> dest, int pieceIndex)
        {
            WriteHeader (dest, MessageType.Suggest, 4);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), pieceIndex);
            return 9;
        }
        public static (Memory<byte>, ByteBufferPool.Releaser) WriteHaveAll ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteHaveAll (buffer.Span));
            return (buffer, releaser);
        }
        public static int WriteHaveAll (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.HaveAll, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteHaveNone ()
        {
            var releaser = MemoryPool.Default.Rent (5, out var buffer);
            buffer = buffer.Slice (0, WriteHaveNone (buffer.Span));
            return (buffer, releaser);
        }

        public static int WriteHaveNone (Span<byte> dest)
        {
            WriteHeader (dest, MessageType.HaveNone, 0);
            return 5;
        }

        public static (Memory<byte>, ByteBufferPool.Releaser) WriteRejectRequest (
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            var releaser = MemoryPool.Default.Rent (5 + 12, out Memory<byte> buffer);
            buffer = buffer.Slice (0, WriteRejectRequest (buffer.Span, pieceIndex, startOffset, requestLength));
            return (buffer, releaser);
        }

        public static int WriteRejectRequest (
            Span<byte> dest,
            int pieceIndex,
            int startOffset,
            int requestLength)
        {
            WriteHeader (dest, MessageType.RejectRequest, 12);

            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), pieceIndex);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (9), startOffset);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (13), requestLength);

            return 17;
        }

        public static int WriteAllowedFast (Span<byte> dest, int pieceIndex)
        {
            WriteHeader (dest, MessageType.AllowedFast, 4);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (5), pieceIndex);
            return 9;
        }

        public static (Memory<byte> message, ByteBufferPool.Releaser releaser) WritePieceHashesFromPieceLayer (MerkleRoot piecesRoot, int fileHashCount, int pieceLength, int index, int? suggestedLength)
        {
            // The layer we're requesting is the 'piece' layer.
            var requestedLayer = BitOps.CeilLog2 (pieceLength / Constants.BlockSize);

            // This should go elsewhere? Layers are *always* powers of two, so round fileHashCount up the to the nearest power of two.
            // An actual file may have 7 hashes, but the layer will have 8.
            var closestPowerOfTwo = (int) BitOps.RoundUpToPowerOf2 (fileHashCount);

            // Never request more than 512 pieces at the same time.
            var preferredLength = suggestedLength.GetValueOrDefault (Math.Min (512, closestPowerOfTwo));

            if (BitOps.PopCount ((uint) preferredLength) != 1)
                throw new ArgumentException ("Value must be a power of 2", nameof (preferredLength));
            if ((index % preferredLength) != 0)
                throw new ArgumentException ("Value must be divisible by preferredLength", nameof (index));
            if (preferredLength > closestPowerOfTwo)
                throw new ArgumentException ("Request length should be less than or equal to hashCount.", nameof (preferredLength));

            // Ensure we don't request padding hashes beyond the end of the layer.
            var length = preferredLength;

            // The number of proofs needed to validate this layer is equal to the number of remaining layers.
            // If we are requesting the whole layer, ask for no proofs.
            var totalProofsRequired = BitOps.CeilLog2 (fileHashCount) - 1;

            return BtEncoder.WriteHashRequest (piecesRoot.Span, requestedLayer, index, length, totalProofsRequired);
        }

        public static (Memory<byte> message, ByteBufferPool.Releaser releaser) WriteHashRequest (
            ReadOnlySpan<byte> piecesRoot,
            int baseLayer,
            int index,
            int length,
            int proofLayers)
        {
            var releaser = MemoryPool.Default.Rent (5 + 32 + 16, out var buffer);
            buffer = buffer.Slice (0, WriteHashRequest (buffer.Span, piecesRoot, baseLayer, index, length, proofLayers));
            return (buffer, releaser);
        }

        public static int WriteHashRequest (
            Span<byte> dest,
            ReadOnlySpan<byte> piecesRoot,
            int baseLayer,
            int index,
            int length,
            int proofLayers)
        {
            int payload = 32 + 16;

            WriteHeader (dest, MessageType.HashRequest, payload);

            piecesRoot.CopyTo (dest.Slice (5));
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (37), baseLayer);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (41), index);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (45), length);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (49), proofLayers);

            return 5 + payload;
        }

        public static (Memory<byte> msg, ByteBufferPool.Releaser releaser) WriteHashes (
           ReadOnlySpan<byte> piecesRoot,
           int baseLayer,
           int index,
           int length,
           int proofLayers,
           ReadOnlySpan<byte> hashes)
        {
            var releaser = MemoryPool.Default.Rent (5 + 32 + 16 + hashes.Length, out var buffer);
            buffer = buffer.Slice (0, WriteHashes (buffer.Span, piecesRoot, baseLayer, index, length, proofLayers, hashes));
            return (buffer, releaser);
        }

        public static int WriteHashes (
            Span<byte> dest,
            ReadOnlySpan<byte> piecesRoot,
            int baseLayer,
            int index,
            int length,
            int proofLayers,
            ReadOnlySpan<byte> hashes)
        {
            int payload = 32 + 16 + hashes.Length;

            WriteHeader (dest, MessageType.Hashes, payload);

            piecesRoot.CopyTo (dest.Slice (5));
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (37), baseLayer);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (41), index);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (45), length);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (49), proofLayers);

            hashes.CopyTo (dest.Slice (53));

            return 5 + payload;
        }

        public static (Memory<byte> msg, ByteBufferPool.Releaser releaser) WriteHashReject (
            ReadOnlySpan<byte> piecesRoot,
            int baseLayer,
            int index,
            int length,
            int proofLayers)
        {
            var releaser = MemoryPool.Default.Rent (5 + 32 + 16, out var buffer);
            buffer = buffer.Slice (0, WriteHashReject (buffer.Span, piecesRoot, baseLayer, index, length, proofLayers));
            return (buffer, releaser);
        }

        public static int WriteHashReject (
            Span<byte> dest,
            ReadOnlySpan<byte> piecesRoot,
            int baseLayer,
            int index,
            int length,
            int proofLayers)
        {
            int payload = 32 + 16;

            WriteHeader (dest, MessageType.HashReject, payload);

            piecesRoot.CopyTo (dest.Slice (5));
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (37), baseLayer);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (41), index);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (45), length);
            BinaryPrimitives.WriteInt32BigEndian (dest.Slice (49), proofLayers);

            return 5 + payload;
        }
    }
}
