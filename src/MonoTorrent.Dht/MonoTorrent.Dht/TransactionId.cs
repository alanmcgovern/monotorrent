//
// TransactionId.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Schema;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht
{
    [InlineArray (2)]
    struct TransactionId : IEquatable<TransactionId>
    {
        byte _bytes;

        static int _current;

        internal static TransactionId From (ReadOnlySpan<byte> span)
        {
            if (span.Length != 2)
                throw new ArgumentException ("Length should be exactly 2", nameof (span));

            var id = new TransactionId ();
            id[0] = span[0];
            id[1] = span[1];
            return id;
        }

        internal static TransactionId From (byte a, byte b)
        {
            var id = new TransactionId ();
            id[0] = a;
            id[1] = b;
            return id;
        }

        public static TransactionId NextId ()
        {
            var val = (ushort) Interlocked.Increment (ref _current);
            var id = new TransactionId ();
            id[0] = (byte) (val >> 8);
            id[1] = (byte) val;
            return id;
        }

        public bool Equals (TransactionId other)
            => this[0] == other[0] && this[1] == other[1];

        public override bool Equals ([NotNullWhen (true)] object? obj)
            => obj is TransactionId id && Equals (id);

        public override int GetHashCode ()
            => this[1] << 8 | this[0];
    }
}
