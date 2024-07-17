//
// HashRequestMessage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Numerics;
using System.Runtime.InteropServices;

namespace MonoTorrent.Messages.Peer
{
    public class HashRequestMessage : PeerMessage
    {
        internal const byte MessageId = 21;
        public override int ByteLength => 4 + 1 + 32 + 4 + 4 + 4 + 4;

        public MerkleRoot PiecesRoot { get; private set; }

        public int BaseLayer { get; private set; }
        public int Index { get; private set; }
        public int Length { get; private set; }
        public int ProofLayers { get; private set; }

        public HashRequestMessage ()
        {

        }

        public HashRequestMessage (MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofLayers)
        {
            PiecesRoot = piecesRoot;
            BaseLayer = baseLayer;
            Index = index;
            Length = length;
            ProofLayers = proofLayers;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PiecesRoot = MerkleRoot.FromMemory (ReadBytes (ref buffer, 32));
            BaseLayer = ReadInt (ref buffer);
            Index = ReadInt (ref buffer);
            Length = ReadInt (ref buffer);
            ProofLayers = ReadInt (ref buffer);
        }

        public override int Encode (Span<byte> buffer)
        {
            var written = buffer.Length;
            Write (ref buffer, ByteLength - 4);
            Write (ref buffer, MessageId);
            Write (ref buffer, PiecesRoot.Span);
            Write (ref buffer, BaseLayer);
            Write (ref buffer, Index);
            Write (ref buffer, Length);
            Write (ref buffer, ProofLayers);
            return written - buffer.Length;
        }

        public override bool Equals (object? obj)
            => obj is HashRequestMessage other
                && MemoryExtensions.SequenceEqual (PiecesRoot.Span, other.PiecesRoot.Span)
                && BaseLayer == other.BaseLayer
                && Index == other.Index
                && Length == other.Length
                && ProofLayers == other.ProofLayers;

        public override int GetHashCode ()
            => MemoryMarshal.Read<int> (PiecesRoot.Span);


        public static HashRequestMessage CreateFromPieceLayer (MerkleRoot piecesRoot, int fileHashCount, int pieceLength, int index, int? suggestedLength)
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
            if(preferredLength > closestPowerOfTwo)
                throw new ArgumentException("Request length should be less than or equal to hashCount.", nameof(preferredLength));

            // Ensure we don't request padding hashes beyond the end of the layer.
            var length = preferredLength;

            // The number of proofs needed to validate this layer is equal to the number of remaining layers.
            // If we are requesting the whole layer, ask for no proofs.
            var totalProofsRequired = BitOps.CeilLog2 (fileHashCount) - 1;

            // YOLO!
            return new HashRequestMessage (piecesRoot, requestedLayer, index, length, totalProofsRequired);
        }

        public override string ToString ()
        {
            var title = $"{nameof (HashRequestMessage)}";
            title += $"{Environment.NewLine}\t File: {BitConverter.ToString (PiecesRoot.Span.ToArray ()).Replace ("-", "")}";
            title += $"{Environment.NewLine}\t BaseLayer: {BaseLayer}";
            title += $"{Environment.NewLine}\t Index: {Index}";
            title += $"{Environment.NewLine}\t Length: {Length}";
            title += $"{Environment.NewLine}\t ProofLayers: {ProofLayers}";
            return title;
        }
    }
}
