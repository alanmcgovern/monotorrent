//
// Torrent.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Security.Cryptography;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    class HashesV2 : ITorrentFileHashProvider
    {
        static readonly Dictionary<int, byte[]> PaddingHashes = new Dictionary<int, byte[]> ();

        static HashesV2 ()
        {
            using var hasher = SHA256.Create ();
            var value = 16 * 1024;
            var hash = new byte[64];
            PaddingHashes[value] = (byte[]) hash.Clone ();
            do {
                value *= 2;

                PaddingHashes[value] = hasher.ComputeHash (hash);
                Array.Copy (hasher.Hash, 0, hash, 0, 32);
                Array.Copy (hasher.Hash, 0, hash, 32, 32);
            } while (value < 1024 * 1024 * 1024);
        }

        Dictionary<BEncodedString, BEncodedString> Layers { get; }

        public int Count { get; } = 421;

        public HashesV2 (Dictionary<BEncodedString, BEncodedString> layers, TorrentFile[] files, int pieceLength)
        {
            Layers = layers;

            ValidateLayers (layers, files, pieceLength);
        }

        private void ValidateLayers (Dictionary<BEncodedString, BEncodedString> layers, TorrentFile[] files, int pieceLength)
        {
            var hasher = SHA256.Create ();

            var maxActualLayerCount = layers.Values.Select (t => t.Span.Length).Max () / 32;
            var maxDesiredLayerCount = (int) Math.Pow (2, Math.Ceiling (Math.Log (maxActualLayerCount, 2)));

            var parentLayer = new byte[maxDesiredLayerCount * 32];
            var currentLayer = new byte[maxDesiredLayerCount * 32];

            for (int counter = 0; counter < files.Length; counter++) {
                if (files[counter].Length < pieceLength)
                    continue;

                var fileLayer = layers[files[counter].PiecesRoot];
                var expectedFileHash = files[counter].PiecesRoot.AsMemory ();

                var actualLayerCount = fileLayer.Span.Length / 32;
                var desiredLayerCount = (int) Math.Pow (2, Math.Ceiling (Math.Log (actualLayerCount, 2)));

                fileLayer.Span.CopyTo (currentLayer);
                for (int i = actualLayerCount; i < desiredLayerCount; i++)
                    Array.Copy (PaddingHashes[pieceLength], 0, currentLayer, i * 32, 32);

                var currentLayerLength = desiredLayerCount * 32;
                while (currentLayerLength != expectedFileHash.Length) {
                    var parentLayerLength = 0;

                    for (int i = 0; i < currentLayerLength / 32; i += 2) {
                        hasher.Initialize ();
#if NETSTANDARD2_1
                        if (!hasher.TryComputeHash (new ReadOnlySpan<byte> (currentLayer, i * 32, 64), new Span<byte> (parentLayer, parentLayerLength, 32), out _))
                            throw new TorrentException ("Could not generate a SHA256 hash");
#else
                        Array.Copy (hasher.ComputeHash (currentLayer, i * 32, 64), 0, parentLayer, parentLayerLength, 32);
#endif
                        parentLayerLength += 32;
                    }

                    (currentLayer, parentLayer) = (parentLayer, currentLayer);
                    currentLayerLength = parentLayerLength;
                }

                for (int i = 0; i < expectedFileHash.Length; i++)
                    if (currentLayer[i] != expectedFileHash.Span[i])
                        throw new TorrentException ($"The data stored in the 'piece layers' field did not match the 'pieces root' for the file {files[counter].Path}");
            }
        }

        public bool IsValid (ReadOnlySpan<byte> hash, int hashIndex)
        {
            throw new System.NotImplementedException ();
        }

        public byte[] ReadHash (int hashIndex)
        {
            throw new System.NotImplementedException ();
        }
    }
}
