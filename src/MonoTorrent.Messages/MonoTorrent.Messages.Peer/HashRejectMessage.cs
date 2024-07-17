//
// HashRejectMessage.cs
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
using System.Runtime.InteropServices;

namespace MonoTorrent.Messages.Peer
{
    public class HashRejectMessage : PeerMessage, IRentable
    {
        internal const byte MessageId = 23;
        public override int ByteLength => 4 + 1 + 32 + 4 + 4 + 4 + 4;

        public MerkleRoot PiecesRoot { get; private set; }

        public int BaseLayer { get; private set; }
        public int Index { get; private set; }
        public int Length { get; private set; }
        public int ProofLayers { get; private set; }

        public HashRejectMessage ()
        {

        }

        public HashRejectMessage (MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofLayers)
            => Initialize (piecesRoot, baseLayer, index, length, proofLayers);

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

        public void Initialize(MerkleRoot piecesRoot, int baseLayer, int index, int length, int proofLayers)
        {
            PiecesRoot = piecesRoot;
            BaseLayer = baseLayer;
            Index = index;
            Length = length;
            ProofLayers = proofLayers;
        }

        public override bool Equals (object? obj)
            => obj is HashRequestMessage other
                && PiecesRoot.Span.SequenceEqual (other.PiecesRoot.Span)
                && BaseLayer == other.BaseLayer
                && Index == other.Index
                && Length == other.Length
                && ProofLayers == other.ProofLayers;

        public override int GetHashCode ()
            => PiecesRoot.GetHashCode ();

        public override string ToString ()
        {
            var title = $"{nameof (HashRejectMessage)}";
            title += $"{Environment.NewLine}\t File: {BitConverter.ToString (PiecesRoot.Span.ToArray ()).Replace ("-", "")}";
            title += $"{Environment.NewLine}\t BaseLayer: {BaseLayer}";
            title += $"{Environment.NewLine}\t Index: {Index}";
            title += $"{Environment.NewLine}\t Length: {Length}";
            title += $"{Environment.NewLine}\t ProofLayers: {ProofLayers}";
            return title;
        }
    }
}
