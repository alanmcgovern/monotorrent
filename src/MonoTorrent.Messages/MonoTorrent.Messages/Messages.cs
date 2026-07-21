using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;

using MonoTorrent.BEncoding;

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
    }

    public readonly ref struct NotInterestedMessage
    {
    }

    public readonly ref struct HaveMessage
    {
        public static readonly int EncodedLength = 9;

        readonly ReadOnlyMemory<byte> _memory;
        public HaveMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct BitfieldMessage
    {
        readonly ReadOnlyMemory<byte> _memory;
        public BitfieldMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public ReadOnlySpan<byte> BitField =>
            _memory.Span.Slice (5);
    }

    public readonly ref struct RequestMessage
    {
        public static readonly int EncodedLength = 17;

        readonly ReadOnlySpan<byte> _span;
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

        readonly ReadOnlyMemory<byte> _memory;

        public PieceMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

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
        readonly ReadOnlyMemory<byte> _memory;
        public CancelMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (5));

        public int StartOffset =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (9));

        public int RequestLength =>
            BinaryPrimitives.ReadInt32BigEndian (Span.Slice (13));
    }

    public readonly ref struct SuggestMessage
    {
        readonly ReadOnlyMemory<byte> _memory;

        public SuggestMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;

        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct RejectRequestMessage
    {
        readonly ReadOnlyMemory<byte> _memory;

        public RejectRequestMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

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

        readonly ReadOnlyMemory<byte> _memory;

        public AllowedFastMessage (ReadOnlyMemory<byte> memory)
            => _memory = memory;


        public int PieceIndex =>
            BinaryPrimitives.ReadInt32BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct HaveAllMessage
    {
    }

    public readonly ref struct HaveNoneMessage
    {
    }

    public readonly ref struct PortMessage
    {
        readonly ReadOnlyMemory<byte> _memory;
        public PortMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        public ushort Port =>
            BinaryPrimitives.ReadUInt16BigEndian (_memory.Span.Slice (5));
    }

    public readonly ref struct HashRequestMessage
    {
        readonly ReadOnlyMemory<byte> _memory;
        public HashRequestMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

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
        readonly ReadOnlyMemory<byte> _memory;
        public HashesMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

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
        readonly ReadOnlyMemory<byte> _memory;
        public HashRejectMessage (ReadOnlyMemory<byte> memory) => _memory = memory;

        ReadOnlySpan<byte> Span => _memory.Span;

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
}
