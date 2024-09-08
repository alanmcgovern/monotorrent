//
// InfoHash.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public sealed class InfoHashes : IEquatable<InfoHashes>
    {
        public static InfoHashes FromInfoHash (InfoHash infoHash)
        {
            if (infoHash is null)
                throw new ArgumentNullException (nameof (infoHash));
            return infoHash.Span.Length == 20
                ? InfoHashes.FromV1 (infoHash)
                : InfoHashes.FromV2 (infoHash);
        }

        public static InfoHashes FromV1 (InfoHash infoHash)
            => new InfoHashes (infoHash, null);

        public static InfoHashes FromV2 (InfoHash infoHash)
            => new InfoHashes (null, infoHash);

        public bool IsHybrid => !(V1 is null) && !(V2 is null);

        /// <summary>
        /// The SHA1 hash of the torrent's 'info' dictionary. Used by V1 torrents and hybrid v1/v2 torrents.
        /// </summary>
        public InfoHash? V1 { get; }

        /// <summary>
        /// The SHA256 hash of the torrent's 'info' dictionary. Used by V2 torrents and hybrid v1/v2 torrents.
        /// </summary>
        public InfoHash? V2 { get; }

        /// <summary>
        /// If the V1 hash is non-null, then it is returned. Otherwise the V2 hash is returned.
        /// As a result, the V1 hash will be returned if both the V1 and V2 hash are non-null.
        /// </summary>
        public InfoHash V1OrV2 => (V1 ?? V2) ?? throw new InvalidOperationException ("Either the V1 or V2 InfoHash must be non-null");

        /// <summary>
        /// Creates an 'InfoHashes' object using the BitTorrent V1 and/or BitTorrent V2 info hashes.
        /// </summary>
        /// <param name="rawInfoHashes"></param>
        public InfoHashes (RawInfoHashes rawInfoHashes)
        {
            V1 = rawInfoHashes.SHA1.IsEmpty ? null : InfoHash.FromMemory (rawInfoHashes.SHA1);
            V2 = rawInfoHashes.SHA256.IsEmpty ? null : InfoHash.FromMemory (rawInfoHashes.SHA256);
        }

        /// <summary>
        /// Creates an 'InfoHashes' object using the BitTorrent V1 and/or BitTorrent V2 info hashes.
        /// </summary>
        /// <param name="sha1InfoHash"></param>
        /// <param name="sha256InfoHash"></param>
        public InfoHashes (InfoHash? sha1InfoHash, InfoHash? sha256InfoHash)
        {
            if (sha1InfoHash is null && sha256InfoHash is null)
                throw new ArgumentNullException ("It is invalid for both 'sha1InfoHash' and 'sha256InfoHash' to both be null.");
            if (!(sha1InfoHash is null) && sha1InfoHash.Span.Length != 20)
                throw new ArgumentException ("Value must be a 20 byte infohash", nameof (sha1InfoHash));
            if (!(sha256InfoHash is null) && sha256InfoHash.Span.Length != 32)
                throw new ArgumentException ("Value must be a 32 byte infohash", nameof (sha256InfoHash));

            V1 = sha1InfoHash;
            V2 = sha256InfoHash;
        }

        public bool Contains (InfoHash infoHash)
            // For V1 torrents we need a direct match.
            // For V2 torrents we need a direct match *or* a substring match. It's 'normal' to send truncated versions
            // of a V2 infohash.
            => V1 == infoHash || (!(V2 is null) && V2.Span.Slice (0, infoHash.Span.Length).SequenceEqual (infoHash.Span));

        public override bool Equals (object? obj)
            => Equals (obj as InfoHashes);

        public bool Equals (InfoHashes? other)
            => !(other is null)
            && other.V1 == V1
            && other.V2 == V2;

        public override int GetHashCode ()
            => V1OrV2.GetHashCode ();

        public int GetMaxByteCount ()
            => (V1 is null ? 0 : 20) + (V2 is null ? 0 : 32);

        public InfoHash Expand (InfoHash infoHash)
        {
            if (infoHash == V1)
                return V1;
            if (V2 != null && infoHash.Span.SequenceEqual (V2.Span.Slice (0, infoHash.Span.Length)))
                return V2;
            throw new ArgumentException("The supplied infohash does not match the V1 or V2 infohash in this object");
        }

        public static bool operator == (InfoHashes? left, InfoHashes? right)
        {
            if (left is null)
                return right is null;
            if (right is null)
                return false;
            return left.V1 == right.V1 && left.V2 == right.V2;
        }

        public static bool operator != (InfoHashes? left, InfoHashes? right)
            => !(left == right);
    }
}
