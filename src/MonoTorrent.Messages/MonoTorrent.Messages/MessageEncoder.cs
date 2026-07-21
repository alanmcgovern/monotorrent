using System;
using System.Buffers.Binary;
using System.Numerics;

namespace MonoTorrent.Messages
{
    public static partial class MessageEncoder
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

            return MessageEncoder.WriteHashRequest (piecesRoot.Span, requestedLayer, index, length, totalProofsRequired);
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
