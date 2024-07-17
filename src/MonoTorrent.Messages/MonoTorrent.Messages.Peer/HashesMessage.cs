//
// HashesMessage.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MonoTorrent.Messages.Peer
{
    public class HashesMessage : PeerMessage, IRentable
    {
        internal const byte MessageId = 22;
        public override int ByteLength => 4 + 1 + 32 + 4 + 4 + 4 + 4 + Hashes.Length;

        public MerkleRoot PiecesRoot { get; private set; }
        public int BaseLayer { get; private set; }
        public int Index { get; private set; }
        public int Length { get; private set; }
        public int ProofLayers { get; private set; }

        public ReadOnlyMemory<byte> Hashes { get; private set; }
        public ByteBufferPool.Releaser HashesReleaser { get; private set; }

        public HashesMessage ()
        {
            Hashes = ReadOnlyMemory<byte>.Empty;
        }

        public HashesMessage (MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofLayers, ReadOnlyMemory<byte> hashes, ByteBufferPool.Releaser hashesReleaser)
            => Initialize (piecesRoot, baseLayer, index, length, proofLayers, hashes, hashesReleaser);

        public void Initialize (MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofLayers, ReadOnlyMemory<byte> hashes, ByteBufferPool.Releaser hashesReleaser)
        {
            PiecesRoot = piecesRoot;
            BaseLayer = baseLayer;
            Index = index;
            Length = length;
            ProofLayers = proofLayers;
            Hashes = hashes;
            HashesReleaser = hashesReleaser;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PiecesRoot = MerkleRoot.FromMemory (ReadBytes (ref buffer, 32));
            BaseLayer = ReadInt (ref buffer);
            Index = ReadInt (ref buffer);
            Length = ReadInt (ref buffer);
            ProofLayers = ReadInt (ref buffer);

            Hashes = ReadBytes (ref buffer, buffer.Length);
        }

        public override int Encode (Span<byte> buffer)
        {
            var original = buffer.Length;
            Write (ref buffer, ByteLength - 4);
            Write (ref buffer, MessageId);
            Write (ref buffer, PiecesRoot.Span);
            Write (ref buffer, BaseLayer);
            Write (ref buffer, Index);
            Write (ref buffer, Length);
            Write (ref buffer, ProofLayers);
            Write (ref buffer, Hashes.Span);
            return original - buffer.Length;
        }

        public override bool Equals (object? obj)
        {
            if (!(obj is HashesMessage other))
                return false;

            if (Hashes.Length != other.Hashes.Length)
                return false;

            if (!MemoryExtensions.SequenceEqual (Hashes.Span, other.Hashes.Span))
                return false;

            return MemoryExtensions.SequenceEqual (PiecesRoot.Span, other.PiecesRoot.Span)
                && BaseLayer == other.BaseLayer
                && Index == other.Index
                && Length == other.Length
                && ProofLayers == other.ProofLayers;
        }

        protected override void Reset ()
        {
            base.Reset ();
            HashesReleaser.Dispose ();
            (Hashes, HashesReleaser) = (default, default);
        }
        public override int GetHashCode ()
            => MemoryMarshal.Read<int> (PiecesRoot.Span);

        public override string ToString ()
        {
            var title = $"{nameof (HashesMessage)}";
            title += $"{Environment.NewLine}\t File: {BitConverter.ToString (PiecesRoot.Span.ToArray ()).Replace ("-", "")}";
            title += $"{Environment.NewLine}\t BaseLayer: {BaseLayer}";
            title += $"{Environment.NewLine}\t Index: {Index}";
            title += $"{Environment.NewLine}\t Length: {Length}";
            title += $"{Environment.NewLine}\t ProofLayers: {ProofLayers}";
            title += $"{Environment.NewLine}\t Data length: {Hashes.Length} ({Hashes.Length / 32} hashes)";
#if NETSTANDARD2_0 || NET472
            title += $"{Environment.NewLine}\t Full message: {Convert.ToBase64String(Encode().ToArray ())})";
#else
            title += $"{Environment.NewLine}\t Full message: {Convert.ToBase64String (Encode ().Span)})";
#endif
            return title;
        }
    }
}
