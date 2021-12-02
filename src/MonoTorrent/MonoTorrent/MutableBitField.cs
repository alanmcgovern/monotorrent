//
// MutableBitField.cs
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

namespace MonoTorrent
{
    public sealed class MutableBitField : BitField
    {
        public new bool this[int index] {
            get => base[index];
            set => base[index] = value;
        }

        public MutableBitField (BitField other)
            : base (other)
        {
        }

        public MutableBitField (ReadOnlySpan<byte> array, int length)
            : base (array, length)
        {
        }

        public MutableBitField (int length)
            : base (length)
        {
        }

        public MutableBitField (bool[] array)
            : base (array)
        {

        }

        public new MutableBitField And (BitField value)
            => (MutableBitField) base.And (value);

        public new MutableBitField From (BitField value)
            => (MutableBitField) base.From (value);

        public new MutableBitField From (ReadOnlySpan<byte> buffer)
            => (MutableBitField) base.From (buffer);

        public new MutableBitField NAnd (BitField value)
            => (MutableBitField) base.NAnd (value);

        public new MutableBitField Not ()
            => (MutableBitField) base.Not ();

        public new MutableBitField Or (BitField value)
            => (MutableBitField) base.Or (value);

        public new MutableBitField Set (int index, bool value)
            => (MutableBitField) base.Set (index, value);

        public new MutableBitField SetAll (bool value)
            => (MutableBitField) base.SetAll (value);

        public MutableBitField SetTrue (int index)
            => (MutableBitField) base.Set (index, true);

        public new MutableBitField SetTrue ((int startPiece, int endPiece) range)
            => (MutableBitField) base.SetTrue (range);

        public new MutableBitField Xor (BitField value)
            => (MutableBitField) base.Xor (value);
    }
}
