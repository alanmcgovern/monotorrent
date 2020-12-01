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
using System.Text;

namespace MonoTorrent
{
    [DebuggerDisplay("InfoHash: (hex) {System.BitConverter.ToString (Hash)}")]
    public class InfoHash : IEquatable<InfoHash>
    {
        static readonly Dictionary<char, byte> Base32DecodeTable;

        static InfoHash ()
        {
            Base32DecodeTable = new Dictionary<char, byte> ();
            const string table = "abcdefghijklmnopqrstuvwxyz234567";
            for (int i = 0; i < table.Length; i++)
                Base32DecodeTable[table[i]] = (byte) i;
        }

        internal byte[] Hash { get; }

        public InfoHash (byte[] infoHash)
        {
            Check.InfoHash (infoHash);
            if (infoHash.Length != 20)
                throw new ArgumentException ("InfoHash must be exactly 20 bytes long", nameof (infoHash));
            Hash = (byte[]) infoHash.Clone ();
        }

        public override bool Equals (object obj)
        {
            return Equals (obj as InfoHash);
        }

        public bool Equals (byte[] other)
        {
            return other != null && other.Length == 20 && Toolbox.ByteMatch (Hash, other);
        }

        public bool Equals (InfoHash other)
        {
            return this == other;
        }

        public override int GetHashCode ()
        {
            // Equality is based generally on checking 20 positions, checking 4 should be enough
            // for the hashcode as infohashes are randomly distributed.
            return Hash[0] | (Hash[1] << 8) | (Hash[2] << 16) | (Hash[3] << 24);
        }

        public byte[] ToArray ()
        {
            return (byte[]) Hash.Clone ();
        }

        public string ToHex ()
        {
            var sb = new StringBuilder (40);
            for (int i = 0; i < Hash.Length; i++) {
                string hex = Hash[i].ToString ("X");
                if (hex.Length != 2)
                    sb.Append ("0");
                sb.Append (hex);
            }
            return sb.ToString ();
        }

        public string UrlEncode ()
        {
            return UriHelper.UrlEncode (Hash);
        }

        public static bool operator == (InfoHash left, InfoHash right)
        {
            if (left is null)
                return right is null;
            if (right is null)
                return false;
            return Toolbox.ByteMatch (left.Hash, right.Hash);
        }

        public static bool operator != (InfoHash left, InfoHash right)
        {
            return !(left == right);
        }

        public static InfoHash FromBase32 (string infoHash)
        {
            Check.InfoHash (infoHash);
            if (infoHash.Length != 32)
                throw new ArgumentException ("InfoHash must be a base32 encoded 32 character string", nameof (infoHash));

            infoHash = infoHash.ToLower ();
            int infoHashOffset = 0;
            byte[] hash = new byte[20];
            byte[] temp = new byte[8];
            for (int i = 0; i < hash.Length;) {
                for (int j = 0; j < 8; j++)
                    if (!Base32DecodeTable.TryGetValue (infoHash[infoHashOffset++], out temp[j]))
                        throw new ArgumentException ("Value is not a valid base32 encoded string", nameof (infoHash));

                //8 * 5bits = 40 bits = 5 bytes
                hash[i++] = (byte) ((temp[0] << 3) | (temp[1] >> 2));
                hash[i++] = (byte) ((temp[1] << 6) | (temp[2] << 1) | (temp[3] >> 4));
                hash[i++] = (byte) ((temp[3] << 4) | (temp[4] >> 1));
                hash[i++] = (byte) ((temp[4] << 7) | (temp[5] << 2) | (temp[6] >> 3));
                hash[i++] = (byte) ((temp[6] << 5) | temp[7]);
            }

            return new InfoHash (hash);
        }

        public static InfoHash FromHex (string infoHash)
        {
            Check.InfoHash (infoHash);
            if (infoHash.Length != 40)
                throw new ArgumentException ("InfoHash must be 40 characters long", nameof (infoHash));

            byte[] hash = new byte[20];
            for (int i = 0; i < hash.Length; i++)
                hash[i] = byte.Parse (infoHash.Substring (i * 2, 2), System.Globalization.NumberStyles.HexNumber);

            return new InfoHash (hash);
        }

        [Obsolete("Use MagnetLink.Parse instead of this method")]
        public static InfoHash FromMagnetLink (string magnetLink)
        => MagnetLink.Parse (magnetLink).InfoHash;

        public static InfoHash UrlDecode (string infoHash)
        {
            Check.InfoHash (infoHash);
            return new InfoHash (UriHelper.UrlDecode (infoHash));
        }
    }
}
