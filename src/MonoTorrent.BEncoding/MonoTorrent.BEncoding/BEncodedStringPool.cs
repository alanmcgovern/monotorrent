//
// BEncodedStringPool.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
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
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Linq;

using Microsoft.VisualBasic;

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Basic string interning when loading/parsing torrents
    /// </summary>
    class BEncodedStringPool : IDisposable
    {
        class Lookup : IEnumerable<BEncodedString>
        {
            const int BucketingFactor = 4095;

            Dictionary<int, List<BEncodedString>> dict = new Dictionary<int, List<BEncodedString>> (32);

            public int MaxLength {
                get; private set;
            }

            public bool TryGetValue (ReadOnlySpan<byte> key, [NotNullWhen(true)] out BEncodedString? value)
            {
                var hc = new HashCode ();
                hc.AddBytes (key);
                var hash = hc.ToHashCode () & BucketingFactor;

                if (dict.TryGetValue (hash, out var list)) {
                    foreach (var s in list) {
                        if (s.Span.SequenceEqual (key)) {
                            value = s;
                            return true;
                        }
                    }
                }
                value = null;
                return false;
            }

            public bool TryGetOrAddValue (ReadOnlySpan<byte> key, out BEncodedString value)
            {
                var hc = new HashCode ();
                hc.AddBytes (key);
                var hash = hc.ToHashCode () & BucketingFactor;

                if (dict.TryGetValue (hash, out var list)) {
                    foreach (var s in list) {
                        if (s.Span.SequenceEqual (key)) {
                            value = s;
                            return true;
                        }
                    }
                } else {
                    dict[hash] = list = new List<BEncodedString> (32);
                }

                value = new BEncodedString (key.ToArray ());
                list.Add (value);
                MaxLength = Math.Max (MaxLength, key.Length);
                return true;
            }

            public IEnumerator<BEncodedString> GetEnumerator ()
            {
                foreach (var list in dict.Values)
                    foreach (var v in list)
                        yield return v;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
            {
                return GetEnumerator ();
            }
        }

        /// <summary>
        /// A BEncodedString intern pool which is read-only. No new instances can be added to it.
        /// </summary>
        public static BEncodedStringPool Instance { get; }

        static readonly Lookup Hardcoded;
        readonly Lookup? Dynamic;
        IMemoryOwner<byte> InterningBuffer;

        static BEncodedStringPool ()
        {
            Hardcoded = new Lookup ();

            // Common .torrent keys
            Hardcoded.TryGetOrAddValue (".pad"u8, out var _);
            Hardcoded.TryGetOrAddValue ("attr"u8, out var _);
            Hardcoded.TryGetOrAddValue ("info"u8, out var _);
            Hardcoded.TryGetOrAddValue ("length"u8, out var _);
            Hardcoded.TryGetOrAddValue ("files"u8, out var _);
            Hardcoded.TryGetOrAddValue ("p"u8, out var _);
            Hardcoded.TryGetOrAddValue ("path"u8, out var _);
            Hardcoded.TryGetOrAddValue ("piece length"u8, out var _);
            Hardcoded.TryGetOrAddValue ("pieces root"u8, out var _);

            // Common DHT keys
            Hardcoded.TryGetOrAddValue ("a"u8, out var _);
            Hardcoded.TryGetOrAddValue ("announce_peer"u8, out var _);
            Hardcoded.TryGetOrAddValue ("e"u8, out var _);
            Hardcoded.TryGetOrAddValue ("find_target"u8, out var _);
            Hardcoded.TryGetOrAddValue ("id"u8, out var _);
            Hardcoded.TryGetOrAddValue ("nodes"u8, out var _);
            Hardcoded.TryGetOrAddValue ("ping"u8, out var _);
            Hardcoded.TryGetOrAddValue ("q"u8, out var _);
            Hardcoded.TryGetOrAddValue ("r"u8, out var _);
            Hardcoded.TryGetOrAddValue ("t"u8, out var _);
            Hardcoded.TryGetOrAddValue ("token"u8, out var _);
            Hardcoded.TryGetOrAddValue ("v"u8, out var _);
            Hardcoded.TryGetOrAddValue ("y"u8, out var _);

            Instance = new BEncodedStringPool (null, null);
        }

        public BEncodedStringPool (IMemoryOwner<byte>? buffer)
            : this (buffer, new Lookup ())
        {

        }

        BEncodedStringPool (IMemoryOwner<byte>? interningBuffer, Lookup? dynamic)
        {
            if (interningBuffer is null)
                interningBuffer = MemoryPool<byte>.Shared.Rent (Hardcoded.MaxLength);

            else if (interningBuffer.Memory.Length < Hardcoded.MaxLength)
                throw new ArgumentException ($"Interning buffer must be at least {Hardcoded.MaxLength} bytes long", nameof (interningBuffer));

            Dynamic = dynamic;
            InterningBuffer = interningBuffer;
        }


        public BEncodedString GetInternedOrCreateNew (Stream reader, int length)
        {
            if (length == 0)
                return BEncodedString.Empty;

            var bytes = length > InterningBuffer.Memory.Length ? new byte[length] : InterningBuffer.Memory.Slice (0, length);

            if (reader.Read (bytes.Span) != length)
                throw new BEncodingException ("Couldn't decode string");

            // Check if the string could be in the hardcoded list, look it up there first.
            if (length < Hardcoded.MaxLength && Hardcoded.TryGetValue (bytes.Span, out var result))
                return result;

            // Otherwise see if we have a local cache and look it up there.
            if (Dynamic is not null && length != 32 && length < InterningBuffer.Memory.Length && Dynamic.TryGetOrAddValue (bytes.Span, out result))
                return result;

            return BEncodedString.FromMemory (length > InterningBuffer.Memory.Length ? bytes : bytes.ToArray ());
        }

        public BEncodedString GetInternedOrCreateNew (ReadOnlySpan<byte> key)
        {
            var length = key.Length;
            if (length == 0)
                return BEncodedString.Empty;

            if (length < Hardcoded.MaxLength && Hardcoded.TryGetValue (key, out var result))
                return result;

            if (Dynamic is not null && length != 32 && length < 128 && Dynamic.TryGetOrAddValue (key, out result))
                return result;

            return BEncodedString.FromMemory (key.ToArray ());
        }

        public void Dispose ()
        {
            InterningBuffer.Dispose ();
        }
    }
}
