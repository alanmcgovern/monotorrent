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
using System.Runtime.InteropServices;

namespace MonoTorrent.Messages.Peer
{
    public class HashesMessage : PeerMessage
    {
        internal static readonly byte MessageId = 22;
        public override int ByteLength => 4 + 1 + 32 + 4 + 4 + 4 + 4 + Hashes.Count * 32;

        public ReadOnlyMemory<byte> PiecesRoot { get; private set; }
        public int BaseLayer { get; private set; }
        public int Index { get; private set; }
        public int Length { get; private set; }
        public int ProofLayers { get; private set; }

        public IList<ReadOnlyMemory<byte>> Hashes { get; private set; }

        public HashesMessage ()
        {
            Hashes = new List<ReadOnlyMemory<byte>> ().AsReadOnly ();
        }

        public HashesMessage (ReadOnlyMemory<byte> piecesRoot, int baseLayer, int index, int length, int proofLayers, IList<ReadOnlyMemory<byte>> hashes)
        {
            PiecesRoot = piecesRoot;
            BaseLayer = baseLayer;
            Index = index;
            Length = length;
            ProofLayers = proofLayers;
            Hashes = hashes;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            PiecesRoot = ReadBytes (ref buffer, 32);
            BaseLayer = ReadInt (ref buffer);
            Index = ReadInt (ref buffer);
            Length = ReadInt (ref buffer);
            ProofLayers = ReadInt (ref buffer);

            var hashes = new List<ReadOnlyMemory<byte>> ();
            while (buffer.Length > 0)
                hashes.Add (ReadBytes (ref buffer, 32));
            Hashes = hashes.AsReadOnly ();
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
            for (int i = 0; i < Hashes.Count; i++)
                Write (ref buffer, Hashes[i].Span);
            return original - buffer.Length;
        }

        public override bool Equals (object? obj)
        {
            if (!(obj is HashesMessage other))
                return false;

            if (Hashes.Count != other.Hashes.Count)
                return false;

            for (int i = 0; i < Hashes.Count; i++)
                if (!MemoryExtensions.SequenceEqual (Hashes[i].Span, other.Hashes[i].Span))
                    return false;

            return MemoryExtensions.SequenceEqual (PiecesRoot.Span, other.PiecesRoot.Span)
                && BaseLayer == other.BaseLayer
                && Index == other.Index
                && Length == other.Length
                && ProofLayers == other.ProofLayers;
        }

        public override int GetHashCode ()
            => MemoryMarshal.Read<int> (PiecesRoot.Span);

        public override string ToString ()
        {
            var title = $"{BitConverter.ToString (PiecesRoot.ToArray ())} - {BaseLayer} - {Index} - {Length} - {ProofLayers}";
            foreach (var hash in Hashes)
                title += Environment.NewLine + BitConverter.ToString (hash.Span.ToArray ());
            return title;
        }
    }
}
