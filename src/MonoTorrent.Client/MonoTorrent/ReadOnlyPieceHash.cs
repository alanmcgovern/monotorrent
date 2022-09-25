//
// ReadOnlyPieceHash.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2022 Alan McGovern
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

namespace MonoTorrent
{
    public readonly struct ReadOnlyPieceHash
    {
        public ReadOnlyMemory<byte> V1Hash { get; }
        public ReadOnlyMemory<byte> V2Hash { get; }

        internal ReadOnlyPieceHash (ReadOnlyMemory<byte> v1Hash, ReadOnlyMemory<byte> v2Hash)
        {
            if (!v1Hash.IsEmpty && v1Hash.Length != 20)
                throw new ArgumentException ("V1 hashes must be 20 bytes long");
            if (!v2Hash.IsEmpty && v2Hash.Length != 32)
                throw new ArgumentNullException ("V2 hashes must be 32 bytes long");

            (V1Hash, V2Hash) = (v1Hash, v2Hash);
        }
    }
}
