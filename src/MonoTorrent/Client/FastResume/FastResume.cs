using System;
using System.IO;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class FastResume
    {
        private static readonly BEncodedString VersionKey = "version";
        private static readonly BEncodedString InfoHashKey = "infohash";
        private static readonly BEncodedString BitfieldKey = "bitfield";
        private static readonly BEncodedString BitfieldLengthKey = "bitfield_length";

        public FastResume()
        {
        }

        public FastResume(InfoHash infoHash, BitField bitfield)
        {
            if (infoHash == null)
                throw new ArgumentNullException("infoHash");
            if (bitfield == null)
                throw new ArgumentNullException("bitfield");

            Infohash = infoHash;
            Bitfield = bitfield;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckContent(dict, VersionKey, 1);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            Infohash = new InfoHash(((BEncodedString) dict[InfoHashKey]).TextBytes);
            Bitfield = new BitField((int) ((BEncodedNumber) dict[BitfieldLengthKey]).Number);
            var data = ((BEncodedString) dict[BitfieldKey]).TextBytes;
            Bitfield.FromArray(data, 0, data.Length);
        }

        public BitField Bitfield { get; }

        public InfoHash Infohash { get; }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key, BEncodedNumber value)
        {
            CheckContent(dict, key);
            if (!dict[key].Equals(value))
                throw new TorrentException(
                    string.Format("Invalid FastResume data. The value of '{0}' was '{1}' instead of '{2}'", key,
                        dict[key], value));
        }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key)
        {
            if (!dict.ContainsKey(key))
                throw new TorrentException(string.Format("Invalid FastResume data. Key '{0}' was not present", key));
        }

        public BEncodedDictionary Encode()
        {
            var dict = new BEncodedDictionary();
            dict.Add(VersionKey, (BEncodedNumber) 1);
            dict.Add(InfoHashKey, new BEncodedString(Infohash.Hash));
            dict.Add(BitfieldKey, new BEncodedString(Bitfield.ToByteArray()));
            dict.Add(BitfieldLengthKey, (BEncodedNumber) Bitfield.Length);
            return dict;
        }

        public void Encode(Stream s)
        {
            var data = Encode().Encode();
            s.Write(data, 0, data.Length);
        }
    }
}