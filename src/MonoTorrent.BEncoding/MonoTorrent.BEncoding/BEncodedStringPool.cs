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

            public void Add (BEncodedString value)
            {
                var hc = new HashCode ();
                hc.AddBytes (value.Span);
                var hash = hc.ToHashCode () & BucketingFactor;

                if (!dict.TryGetValue (hash, out var list))
                    dict[hash] = list = new List<BEncodedString> (32);
                list.Add (value);
                MaxLength = Math.Max (MaxLength, value.Span.Length);
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
            Hardcoded = new Lookup {
                // Common .torrent keys
                ".pad",
                "attr",
                "info",
                "length",
                "files",
                "p",
                "path",
                "piece length",
                "pieces root",

                // Common DHT keys
                "a",
                "announce_peer",
                "e",
                "find_target",
                "id",
                "nodes",
                "ping",
                "q",
                "r",
                "t",
                "token",
                "v",
                "y",
            };

            Instance = new BEncodedStringPool (null);
        }

        public BEncodedStringPool (IMemoryOwner<byte>? buffer)
            : this (new Lookup (), buffer)
        {

        }

        BEncodedStringPool (Lookup? dynamic, IMemoryOwner<byte>? interningBuffer)
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
            if (Dynamic is not null && Dynamic.TryGetValue (bytes.Span, out result))
                return result;

            // It wasn't in a cache, so create a new instance. Add it to the cache if there is one.
            // This is a caching instance, so add it now.
            result = BEncodedString.FromMemory (length > InterningBuffer.Memory.Length ? bytes : bytes.ToArray ());

            // Don't add 32 byte results to the dynamic cache, as these are likely to be infohashes
            // and there are likely to be many of them.
            if (Dynamic is not null && length != 32 &&  length < InterningBuffer.Memory.Length)
                Dynamic.Add (result);
            return result;
        }

        public BEncodedString GetInternedOrCreateNew (ReadOnlySpan<byte> key)
        {
            if (key.Length == 0)
                return BEncodedString.Empty;

            if (Hardcoded.TryGetValue (key, out var result))
                return result;

            if (Dynamic is not null && Dynamic.TryGetValue (key, out result))
                return result;

            result = BEncodedString.FromMemory (key.ToArray ());

            // Don't add 32 byte results to the dynamic cache, as these are likely to be infohashes
            // and there are likely to be many of them. Don't add really large items either.
            if (Dynamic is not null && result.Span.Length != 32 && result.Span.Length < 128)
                Dynamic.Add (result);
            return result;
        }

        public void Dispose ()
        {
            InterningBuffer.Dispose ();
        }
    }
}
