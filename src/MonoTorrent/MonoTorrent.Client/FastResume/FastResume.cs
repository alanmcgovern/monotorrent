using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.IO;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    public class FastResume
    {
        private static readonly BEncodedString VersionKey = (BEncodedString)"version";
        private static readonly BEncodedString InfoHashKey = (BEncodedString)"infohash";
        private static readonly BEncodedString BitfieldKey = (BEncodedString)"bitfield";
        private static readonly BEncodedString BitfieldLengthKey = (BEncodedString)"bitfield_length";

        private BitField bitfield;
        private byte[] infoHash;
        private List<Peer> peers;

        public BitField Bitfield
        {
            get { return bitfield; }
        }

        public byte[] InfoHash
        {
            get { return infoHash; }
        }

        public FastResume()
        {
        }

        public FastResume(byte[] infoHash, BitField bitfield, List<Peer> peers)
        {
            if (infoHash==null)
                throw new ArgumentNullException("infoHash");
            if(bitfield == null)
                throw new ArgumentNullException("bitfield");
            if(peers == null)
                throw new ArgumentNullException("peers");

            this.infoHash = infoHash;
            this.bitfield = bitfield;
            this.peers = peers;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckContent(dict, VersionKey, (BEncodedNumber)1);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            infoHash = ((BEncodedString)dict[InfoHashKey]).TextBytes;
            bitfield = new BitField((int)((BEncodedNumber)dict[BitfieldLengthKey]).Number);
            byte[] data = ((BEncodedString)dict[BitfieldKey]).TextBytes;
            bitfield.FromArray(data, 0, data.Length);
        }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key, BEncodedNumber value)
        {
            CheckContent(dict, key);
            if (!dict[key].Equals(value))
                throw new TorrentException(string.Format("Invalid FastResume data. The value of '{0}' was '{1}' instead of '{2}'", key, dict[key], value));
        }

        private void CheckContent(BEncodedDictionary dict, BEncodedString key)
        {
            if (!dict.ContainsKey(key))
                throw new TorrentException(string.Format("Invalid FastResume data. Key '{0}' was not present", key));
        }

        public BEncodedDictionary Encode()
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            dict.Add(VersionKey, (BEncodedNumber)1);
            dict.Add(InfoHashKey, new BEncodedString(infoHash));
            dict.Add(BitfieldKey, new BEncodedString(bitfield.ToByteArray()));
            dict.Add(BitfieldLengthKey, (BEncodedNumber)bitfield.Length);
            return dict;
        }

        public void Encode(Stream s)
        {
            byte[] data = Encode().Encode();
            s.Write(data, 0, data.Length);
        }
    }
}
