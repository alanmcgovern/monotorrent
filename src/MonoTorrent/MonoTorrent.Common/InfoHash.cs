using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Web;

namespace MonoTorrent
{
    public class InfoHash : IEquatable <InfoHash>
    {
        byte[] hash;

        internal byte[] Hash
        {
            get { return hash; }
        }

        public InfoHash(byte[] infoHash)
        {
            Check.InfoHash(infoHash);
            if (infoHash.Length != 20)
                throw new ArgumentException("Infohash must be exactly 20 bytes long");
            hash = (byte[])infoHash.Clone();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InfoHash);
        }

        public bool Equals(byte[] other)
        {
            return other == null || other.Length != 20 ? false : Toolbox.ByteMatch(Hash, other);
        }

        public bool Equals(InfoHash other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return Toolbox.HashCode(hash);
        }

        public string ToHex()
        {
            return Toolbox.ToHex(Hash);
        }

        public override string ToString()
        {
            return BitConverter.ToString(hash);
        }

        public string UrlEncode()
        {
            return HttpUtility.UrlEncode(Hash);
        }

        public static bool operator ==(InfoHash left, InfoHash right)
        {
            if ((object)left == null)
                return (object)right == null;
            if ((object)right == null)
                return false;
            return Toolbox.ByteMatch(left.Hash, right.Hash);
        }

        public static bool operator !=(InfoHash left, InfoHash right)
        {
            return !(left == right);
        }

        public static InfoHash UrlDecode(string infoHash)
        {
            Check.InfoHash(infoHash);
            return new InfoHash(HttpUtility.UrlDecodeToBytes(infoHash));
        }
    }
}
