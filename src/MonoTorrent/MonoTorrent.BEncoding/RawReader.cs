//
// RawReader.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.IO;

namespace MonoTorrent.BEncoding
{
    public class RawReader : Stream
    {
        readonly Stream input;
        readonly byte[] peeked;

        public bool StrictDecoding { get; }

        public RawReader (Stream input)
            : this (input, true)
        {

        }

        public RawReader (Stream input, bool strictDecoding)
        {
            this.input = input;
            peeked = new byte[1];
            StrictDecoding = strictDecoding;
        }

        public override bool CanRead => input.CanRead;

        public override bool CanSeek => input.CanSeek;

        public override bool CanWrite => false;

        public override long Length => input.Length;

        public override long Position {
            get => input.Position;
            set {
                if (value != Position)
                    input.Position = value;
            }
        }

        public override void Flush ()
            => throw new NotSupportedException ();

        public override int ReadByte ()
            => Read (peeked, 0, 1) == 1 ? peeked[0] : -1;

        public override int Read (byte[] buffer, int offset, int count)
            => input.Read (buffer, offset, count);

        public override long Seek (long offset, SeekOrigin origin)
            => input.Seek (offset, origin);

        public override void SetLength (long value)
            =>  throw new NotSupportedException ();

        public override void Write (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();
    }
}
