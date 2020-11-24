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
        bool hasPeek;
        readonly Stream input;
        readonly byte[] peeked;

        MemoryStream CapturedData { get; set; }

        public bool StrictDecoding { get; }

        public RawReader (Stream input)
            : this (input, false)
        {

        }

        public RawReader (Stream input, bool strictDecoding)
        {
            this.input = input;
            peeked = new byte[1];
            StrictDecoding = strictDecoding;
        }

        internal void BeginCaptureData (MemoryStream stream)
        {
            CapturedData = stream;
        }

        internal void EndCaptureData ()
        {
            CapturedData = null;
        }

        public override bool CanRead => input.CanRead;

        public override bool CanSeek => input.CanSeek;

        public override bool CanWrite => false;

        public override void Flush ()
        {
            throw new NotSupportedException ();
        }

        public override long Length => input.Length;

        public int PeekByte ()
        {
            if (!hasPeek)
                hasPeek = Read (peeked, 0, 1) == 1;
            return hasPeek ? peeked[0] : -1;
        }

        public override int ReadByte ()
        {
            var result = PeekByte ();
            hasPeek = false;
            return result;
        }

        public override long Position {
            get {
                if (hasPeek)
                    return input.Position - 1;
                return input.Position;
            }
            set {
                if (value != Position) {
                    hasPeek = false;
                    Seek (value, SeekOrigin.Begin);
                }
            }
        }

        public override int Read (byte[] buffer, int offset, int count)
        {
            int read = 0;
            if (hasPeek && count > 0) {
                hasPeek = false;
                buffer[offset] = peeked[0];
                offset++;
                count--;
                read++;
            }

            var actuallyRead = input.Read (buffer, offset, count);
            if (actuallyRead > 0)
                CapturedData?.Write (buffer, offset, actuallyRead);
            return read + actuallyRead;
        }

        public override long Seek (long offset, SeekOrigin origin)
        {
            if (CapturedData != null)
                throw new NotSupportedException ("Cannot seek while capturing data");
            long val;
            if (hasPeek && origin == SeekOrigin.Current)
                val = input.Seek (offset - 1, origin);
            else
                val = input.Seek (offset, origin);
            hasPeek = false;
            return val;
        }

        public override void SetLength (long value)
        {
            throw new NotSupportedException ();
        }

        public override void Write (byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException ();
        }
    }
}
