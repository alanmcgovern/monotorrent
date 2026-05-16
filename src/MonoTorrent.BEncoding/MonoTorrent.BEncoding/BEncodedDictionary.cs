//
// BEncodedDictionary.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Class representing a BEncoded Dictionary
    /// </summary>
    public class BEncodedDictionary : BEncodedValue, IDictionary<BEncodedString, BEncodedValue>
    {
        /// <summary>
        /// Special decoding method for torrent files. This mode will ensure the correct infohash is generated
        /// for torrents which contain dictionaries with misordered keys.
        /// </summary>
        /// <returns></returns>
        public static (BEncodedDictionary torrent, RawInfoHashes infoHashes) DecodeTorrent (ReadOnlySpan<byte> buffer)
            => BEncodeDecoder.DecodeTorrent (ref buffer);

        /// <summary>
        /// Special decoding method for torrent files. This mode will ensure the correct infohash is generated
        /// for torrents which contain dictionaries with misordered keys.
        /// </summary>
        /// <returns></returns>
        public static (BEncodedDictionary torrent, RawInfoHashes infohashes) DecodeTorrent (Stream stream)
            => BEncodeDecoder.DecodeTorrent (stream);

        KeyValuePair<BEncodedString, BEncodedValue>[] items;

        public int Count { get; private set; }

        bool ICollection<KeyValuePair<BEncodedString, BEncodedValue>>.IsReadOnly => false;

        /// <summary>
        /// Returns a snapshot of the keys in this dictionary. This is a copy of the keys, so modifying the returned collection will not affect the dictionary.
        /// </summary>
        ICollection<BEncodedString> IDictionary<BEncodedString, BEncodedValue>.Keys {
            get {
                var arr = new BEncodedString[Count];
                for (int i = 0; i < Count; i++)
                    arr[i] = items[i].Key;
                return arr;
            }
        }

        /// <summary>
        /// Returns a snapshot of the values in this dictionary. This is a copy of the values, so modifying the returned collection will not affect the dictionary.
        /// </summary>
        ICollection<BEncodedValue> IDictionary<BEncodedString, BEncodedValue>.Values {
            get {
                var arr = new BEncodedValue[Count];
                for (int i = 0; i < Count; i++)
                    arr[i] = items[i].Value;
                return arr;
            }
        }

        public BEncodedValue this[BEncodedString key] {
            get {
                int index = BinarySearch (key);
                if (index < 0)
                    throw new KeyNotFoundException ();

                return items[index].Value;
            }
            set {
                int index = BinarySearch (key);

                if (index >= 0) {
                    items[index] = new (key, value);
                    return;
                }

                Insert (~index, key, value);
            }
        }

        public BEncodedDictionary ()
        {
            items = new KeyValuePair<BEncodedString, BEncodedValue>[4];
        }

        public void Add (BEncodedString key, BEncodedValue value)
        {
            int index = BinarySearch (key);
            if (index >= 0)
                throw new ArgumentException ("Duplicate key");

            Insert (~index, key, value);
        }

        Memory<KeyValuePair<BEncodedString, BEncodedValue>> AsMemory ()
            => items.AsMemory (0, Count);

        Span<KeyValuePair<BEncodedString, BEncodedValue>> AsSpan ()
            => items.AsSpan (0, Count);

        private int BinarySearch (BEncodedString key)
        {
            int lo = 0, hi = Count - 1;
            while (lo <= hi) {
                int mid = (lo + hi) >> 1;
                int cmp = items[mid].Key.CompareTo (key);

                if (cmp == 0)
                    return mid;
                if (cmp < 0)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return ~lo;
        }

        public void Clear ()
        {
            Array.Clear (items, 0, Count);
            Count = 0;
        }

        public bool ContainsKey (BEncodedString key)
            => BinarySearch (key) >= 0;

        public void Add (KeyValuePair<BEncodedString, BEncodedValue> item)
            => Add (item.Key, item.Value);

        public bool Contains (KeyValuePair<BEncodedString, BEncodedValue> item)
            => TryGetValue (item.Key, out var v) && v.Equals (item.Value);

        public void CopyTo (KeyValuePair<BEncodedString, BEncodedValue>[] array, int arrayIndex)
            => AsSpan ().CopyTo (array.AsSpan (arrayIndex));

        public override bool Equals (object? obj)
        {
            if (ReferenceEquals (this, obj))
                return true;

            if (obj is not BEncodedDictionary other)
                return false;

            if (Count != other.Count)
                return false;

            foreach (ref var item in AsSpan ()) {
                if (!other.TryGetValue (item.Key, out var v))
                    return false;

                if (!item.Value.Equals (v))
                    return false;
            }

            return true;
        }

        public override int Encode (Span<byte> buffer)
        {
            buffer[0] = (byte) 'd';
            int written = 1;

            foreach (ref var item in AsSpan ()) {
                written += item.Key.Encode (buffer.Slice (written));
                written += item.Value.Encode (buffer.Slice (written));
            }

            buffer[written++] = (byte) 'e';
            return written;
        }

        private void EnsureCapacity (int size)
        {
            if (size <= items.Length)
                return;

            int newSize = items.Length * 2;
            if (newSize < size)
                newSize = size;

            Array.Resize (ref items, newSize);
        }

        public override int LengthInBytes ()
        {
            // the 'd' and 'e' for the dictionary
            int length = 2;

            foreach (ref var item in AsSpan ()) {
                length += item.Key.LengthInBytes ();
                length += item.Value.LengthInBytes ();
            }

            return length;
        }

        public Enumerator GetEnumerator ()
            => new Enumerator (AsMemory ());

        public BEncodedValue? GetValueOrDefault (BEncodedString key)
            => TryGetValue (key, out BEncodedValue? value) ? value : null;

        public BEncodedValue? GetValueOrDefault (BEncodedString key, BEncodedValue? defaultValue)
            => TryGetValue (key, out BEncodedValue? value) ? value : defaultValue;

        IEnumerator<KeyValuePair<BEncodedString, BEncodedValue>> IEnumerable<KeyValuePair<BEncodedString, BEncodedValue>>.GetEnumerator ()
            => GetEnumerator ();

        IEnumerator IEnumerable.GetEnumerator ()
            => GetEnumerator ();

        public override int GetHashCode ()
        {
            var hc = new HashCode ();
            foreach (ref var item in AsSpan ()) {
                hc.Add (item.Key.GetHashCode ());
                hc.Add (item.Value.GetHashCode ());
            }
            return hc.ToHashCode ();
        }

        private void Insert (int index, BEncodedString key, BEncodedValue value)
        {
            EnsureCapacity (Count * 2);

            if (index < Count) {
                Array.Copy (items, index, items, index + 1, Count - index);
            }

            items[index] = new (key, value);
            Count++;
        }

        public bool Remove (BEncodedString key)
        {
            int index = BinarySearch (key);
            if (index < 0)
                return false;

            Array.Copy (items, index + 1, items, index, Count - index - 1);
            Count--;
            items[Count] = default;
            return true;
        }

        public bool Remove (KeyValuePair<BEncodedString, BEncodedValue> item)
            => Remove (item.Key);

        public bool TryGetValue (BEncodedString key, [MaybeNullWhen (false)] out BEncodedValue value)
        {
            int index = BinarySearch (key);
            if (index >= 0) {
                value = items[index].Value;
                return true;
            }

            value = null!;
            return false;
        }

        public override string ToString ()
            => $"BEncodedDictionary [{Count} items]";

        public struct Enumerator : IEnumerator<KeyValuePair<BEncodedString, BEncodedValue>>
        {
            private readonly ReadOnlyMemory<KeyValuePair<BEncodedString, BEncodedValue>> items;
            private int index;
            public KeyValuePair<BEncodedString, BEncodedValue> Current
                => items.Span[index];

            object IEnumerator.Current => Current;

            internal Enumerator (ReadOnlyMemory<KeyValuePair<BEncodedString, BEncodedValue>> items)
            {
                this.items = items;
                this.index = -1;
            }

            public bool MoveNext ()
                => ++index < items.Length;

            void IDisposable.Dispose ()
            {
            }

            void IEnumerator.Reset ()
                => index = -1;
        }
    }
}
