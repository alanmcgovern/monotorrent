//
// BitFieldData.cs
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
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MonoTorrent
{
    /// <summary>
    /// This class is for represting the Peer's bitfield
    /// </summary>
    [DebuggerDisplay ("{" + nameof (ToDebuggerString) + " ()}")]
    readonly struct BitFieldData : IEnumerable<bool>
    {
        const int StorageByteCount = sizeof (ulong);
        const int StorageBitCount = StorageByteCount * 8;

        ulong[]? _raw { get; }

        public Span<ulong> Span => _raw.AsSpan (1);

        public bool AllFalse => TrueCount == 0;

        public bool AllTrue => TrueCount == Length;

        public int Length {
            get => _raw  is null ? 0 : (int) (_raw[0] >> 32);
            private set => _raw![0] = ((ulong) (uint) value << 32) | (uint) TrueCount;
        }

        public int LengthInBytes => (Length + 7) / 8;

        public double PercentComplete => (double) TrueCount / Length * 100.0;

        public int TrueCount {
            get => _raw is null ? 0 : (int) (_raw[0] & 0xFFFFFFFF);
            private set => _raw![0] = ((ulong) (uint) Length << 32) | (uint) value;
        }

        internal void From (ReadOnlySpan<byte> buffer)
        {
            int end = Length / StorageBitCount;
            for (int i = 0; i < end; i++) {
                Span[i] = BinaryPrimitives.ReadUInt64BigEndian (buffer);
                buffer = buffer.Slice (StorageByteCount);
            }

            int shift = StorageBitCount - 8;
            for (int i = end * StorageBitCount; i < Length; i += 8) {
                Span[Span.Length - 1] |= (ulong) buffer[0] << shift;
                buffer = buffer.Slice (1);
                shift -= 8;
            }
            ZeroUnusedBits ();

            int count = 0;
            for (int i = 0; i < Span.Length; i++)
                count += BitOps.PopCount (Span[i]);
            TrueCount = count;
        }

        internal void ZeroUnusedBits ()
        {
            int shift = Length % StorageBitCount;
            if (shift != 0)
                Span[Span.Length - 1] &= ulong.MaxValue << (StorageBitCount - shift);
        }


        public BitFieldData (int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException (nameof (length), "Length must be greater than zero");

            _raw = new ulong[(length + StorageBitCount - 1) / StorageBitCount + 1];
            Length = length;
        }

        public BitFieldData (BitFieldData other)
        {
            _raw = (ulong[]?) other._raw?.Clone ();
        }

        public BitFieldData (ReadOnlySpan<byte> buffer, int length)
            : this (length)
        {
            if ((length + StorageBitCount - 1) / StorageBitCount > buffer.Length)
                throw new ArgumentOutOfRangeException ("The buffer was too small");
            From (buffer);
        }

        public BitFieldData (bool[] array)
            : this (array?.Length ?? throw new ArgumentNullException (nameof (array)))
        {
            for (int i = 0; i < array.Length; i++)
                Set (i, array[i]);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool Get (int index)
        {
            if ((uint) index >= (uint) Length)
                throw new ArgumentOutOfRangeException (nameof (index));

            if (AllTrue)
                return true;

            int i = index >> 6;
            ulong mask = 1ul << (63 - (index & 63));
            return (Span[i] & mask) != 0;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Set (int index, bool value)
        {
            if ((uint) index >= (uint) Length)
                throw new ArgumentOutOfRangeException (nameof (index));

            int i = index >> 6;
            ulong mask = 1ul << (63 - (index & 63));
            ref ulong word = ref Span[i];

            if (value) {
                if ((word & mask) == 0) {
                    TrueCount++;
                    word |= mask;
                }
            } else {
                if ((word & mask) != 0) {
                    TrueCount--;
                    word &= ~mask;
                }
            }
        }

        public override bool Equals (object? obj)
            => obj is BitFieldData other
            && _raw.AsSpan ().SequenceEqual (other._raw);

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit in the bitfield.
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstTrue ()
            => FirstTrue (0, Length - 1);

        /// <summary>
        /// Returns the index of the first <see langword="true" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="true" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstTrue (int startIndex, int endIndex)
        {
            if (startIndex < 0 || startIndex >= Length)
                throw new IndexOutOfRangeException (nameof (startIndex));
            if (endIndex < 0 || endIndex >= Length)
                throw new IndexOutOfRangeException (nameof (endIndex));

            if (AllTrue)
                return startIndex;
            if (AllFalse)
                return -1;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / StorageBitCount), Span.Length - 1);
            int loopStart = startIndex / StorageBitCount;
            for (int i = loopStart; i <= loopEnd; i++) {
                ref ulong current = ref Span[i];
                if (current == 0) {        // This one has no true values
                    var next = Span.Slice (i + 1).IndexOfAnyExcept (0ul);
                    if (next != -1)
                        i += next;
                    continue;
                }

                int start = i * StorageBitCount;
                int end = start + (StorageBitCount - 1);

                var mask = ulong.MaxValue;
                if (start < startIndex)
                    mask &= ulong.MaxValue >> (startIndex - start);
                if (end > endIndex)
                    mask &= ulong.MaxValue << (end - endIndex);
                mask &= current;
                if (mask == 0)
                    continue;
                return System.Numerics.BitOperations.LeadingZeroCount (mask) + start;
            }

            return -1;              // Nothing is true
        }

        internal void From (BitFieldData value)
        {
            Check (value);
            if (_raw is not null)
                value._raw!.CopyTo (_raw.AsSpan ());
        }

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit in the bitfield.
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <returns></returns>
        public int FirstFalse ()
            => FirstFalse (0, Length - 1);

        /// <summary>
        /// Returns the index of the first <see langword="false" /> bit between <paramref name="startIndex"/> and <paramref name="endIndex"/> (inclusive).
        /// Returns -1 if no <see langword="false" /> bit is found. />
        /// </summary>
        /// <param name="startIndex">The first index to check</param>
        /// <param name="endIndex">The last index to check</param>
        /// <returns></returns>
        public int FirstFalse (int startIndex, int endIndex)
        {
            if (startIndex < 0 || startIndex >= Length)
                throw new IndexOutOfRangeException (nameof (startIndex));
            if (endIndex < 0 || endIndex >= Length)
                throw new IndexOutOfRangeException (nameof (endIndex));

            int start;
            int end;

            if (AllTrue)
                return -1;
            if (AllFalse)
                return startIndex;

            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            int loopEnd = Math.Min ((endIndex / StorageBitCount), Span.Length - 1);
            int loopStart = startIndex / StorageBitCount;
            for (int i = loopStart; i <= loopEnd; i++) {
                ref ulong current = ref Span[i];
                if (current == ulong.MaxValue && i != loopEnd) {        // This one has no false values
                    var next = Span.Slice (i + 1).IndexOfAnyExcept (ulong.MaxValue);
                    if (next != -1)
                        i += next;
                    continue;
                }
                start = i * StorageBitCount;
                end = start + StorageBitCount - 1;

                var mask = ulong.MaxValue;
                if (start < startIndex)
                    mask &= ulong.MaxValue >> (startIndex - start);
                if (end > endIndex)
                    mask &= ulong.MaxValue << (end - endIndex);
                mask &= ~current;
                if (mask == 0)
                    continue;
                return System.Numerics.BitOperations.LeadingZeroCount (mask) + start;
            }

            return -1;              // Nothing is true
        }


        public IEnumerator<bool> GetEnumerator ()
        {
            for (int i = 0; i < Length; i++)
                yield return Get (i);
        }

        public int CountTrue (BitFieldData selector)
        {
            Check (selector);

            int count = 0;
            var data = Span;
            var selectorData = selector.Span;
            for (int i = 0; i < data.Length && i < selectorData.Length; i++)
                count += BitOps.PopCount (data[i] & selectorData[i]);
            return count;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public override int GetHashCode ()
        {
            HashCode hc = new HashCode ();
            hc.AddBytes (MemoryMarshal.Cast<ulong, byte> (Span));
            return hc.ToHashCode ();
        }

        public bool SequenceEqual(BitFieldData data)
            => (_raw ?? Array.Empty<ulong> ()).SequenceEqual (data._raw ?? Array.Empty<ulong> ());

        public byte[] ToBytes ()
        {
            byte[] data = new byte[LengthInBytes];
            ToBytes (data);
            return data;
        }

        public int ToBytes (Span<byte> buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException (nameof (buffer));
            if (buffer.Length < LengthInBytes)
                throw new ArgumentOutOfRangeException ($"The buffer must be able to store at least {LengthInBytes} bytes");

            int end = Length / 64;
            int offset = 0;
            for (int i = 0; i < end; i++) {
                buffer[offset++] = (byte) (Span[i] >> 56);
                buffer[offset++] = (byte) (Span[i] >> 48);
                buffer[offset++] = (byte) (Span[i] >> 40);
                buffer[offset++] = (byte) (Span[i] >> 32);
                buffer[offset++] = (byte) (Span[i] >> 24);
                buffer[offset++] = (byte) (Span[i] >> 16);
                buffer[offset++] = (byte) (Span[i] >> 8);
                buffer[offset++] = (byte) (Span[i] >> 0);
            }

            int shift = StorageBitCount - 8;
            for (int i = end * StorageBitCount; i < Length; i += 8) {
                buffer[offset++] = (byte) (Span[Span.Length - 1] >> shift);
                shift -= 8;
            }

            return offset;
        }

        public void SetTrue ((int startPiece, int endPiece) range)
        {
            for (int i = range.startPiece; i <= range.endPiece; i++)
                Set(i, true);
        }

        public void SetTrue (params int[] indices)
        {
            foreach (int index in indices)
                Set(index, true);
        }

        public void SetFalse (params int[] indices)
        {
            foreach (int index in indices)
                Set(index, false);
        }

        public void SetAll (bool value)
        {
            if ((TrueCount == Length && value) || (!value && TrueCount == 0))
                return;

            if (value) {
                Span.Fill (ulong.MaxValue);
                ZeroUnusedBits ();
                TrueCount = Length;
            } else {
                Span.Fill (0);
                TrueCount = 0;
            }
        }

        public void Not ()
        {
            var data = Span;
            for (int i = 0; i < data.Length; i++)
                data[i] = ~data[i];

            ZeroUnusedBits ();
            TrueCount = Length - TrueCount;
        }

        public void And (BitFieldData value)
        {
            Check (value);

            var data = Span;
            var valueData = value.Span;
            int count = 0;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] & valueData[i];
                count += BitOps.PopCount (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        public void NAnd (BitFieldData value)
        {
            Check (value);

            if (AllFalse || value.AllFalse)
                return;

            if (value.AllTrue) {
                SetAll (false);
            } else {
                int count = 0;
                ref ulong data = ref Span[0];
                ref ulong dataEnd = ref Unsafe.Add (ref data, Span.Length);
                ref ulong valueData = ref MemoryMarshal.GetReference (value.Span);
                do {
                    var result = data & ~valueData;
                    count += BitOps.PopCount (result);
                    data = result;

                    data = ref Unsafe.Add (ref data, 1);
                    valueData = ref Unsafe.Add (ref valueData, 1);
                } while (Unsafe.IsAddressLessThan (ref data, ref dataEnd));
                TrueCount = count;
            }
        }

        public void Or (BitFieldData value)
        {
            Check (value);

            int count = 0;
            var data = Span;
            var valueData = value.Span;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] | valueData[i];
                count += BitOps.PopCount (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        public void Xor (BitFieldData value)
        {
            Check (value);

            int count = 0;
            var data = Span;
            var valueData = value.Span;
            for (int i = 0; i < data.Length && i < valueData.Length; i++) {
                var result = data[i] ^ valueData[i];
                count += BitOps.PopCount (result);
                data[i] = result;
            }
            TrueCount = count;
        }

        [ExcludeFromCodeCoverage]
        string ToDebuggerString ()
        {
            var sb = new StringBuilder (Span.Length * 16);
            for (int i = 0; i < Length; i++) {
                sb.Append (Get (i) ? 'T' : 'F');
                sb.Append (' ');
            }

            return sb.ToString (0, sb.Length - 1);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        void Check (BitFieldData value)
        {
            if (Length != value.Length)
                throw new ArgumentException ("BitFields are of different lengths", nameof (value));
        }
    }
}
