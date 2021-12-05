﻿//
// RequestBundle.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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

namespace MonoTorrent.Messages.Peer
{
    public class HaveBundle : PeerMessage
    {
        static int HaveMessageLength = new HaveMessage ().ByteLength;

        List<int> PieceIndexes { get; }

        public override int ByteLength => HaveMessageLength * PieceIndexes.Count;

        public int Count => PieceIndexes.Count;

        public HaveBundle ()
        {
            PieceIndexes = new List<int> ();
        }

        public void Add (int index)
            => PieceIndexes.Add (index);

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            using (Rent (out HaveMessage message)) {
                for (int i = 0; i < PieceIndexes.Count; i++) {
                    message.PieceIndex = PieceIndexes[i];
                    buffer = buffer.Slice (message.Encode (buffer));
                }
            }
            return written - buffer.Length;
        }

        public void Initialize (IList<int> haves)
        {
            PieceIndexes.AddRange (haves);
        }

        protected override void Reset ()
        {
            base.Reset ();
            PieceIndexes.Clear ();
        }
    }
}
