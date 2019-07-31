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

        private static readonly BEncodedString ActivePeersKey         = (BEncodedString)"ActivePeers";
        //private static readonly BEncodedString ActivePeers_LengthKey  = (BEncodedString)"ActivePeers_Length";
        private static readonly BEncodedString AvailablePeersKey      = (BEncodedString)"AvailablePeers";
        //private static readonly BEncodedString AvailablePeers_LengthKey = (BEncodedString)"AvailablePeers_Length";
        private static readonly BEncodedString BannedPeersKey         = (BEncodedString)"BannedPeers";
        //private static readonly BEncodedString BannedPeers_LengthKey  = (BEncodedString)"BannedPeers_Length";
        private static readonly BEncodedString BusyPeersKey           = (BEncodedString)"BusyPeers";
        //private static readonly BEncodedString BusyPeers_LengthKey    = (BEncodedString)"BusyPeers_Length";

        private BitField bitfield;
        private InfoHash infoHash;

        private List<Peer> activePeers;
        private List<Peer> availablePeers;
        private List<Peer> bannedPeers;
        private List<Peer> busyPeers;

        public BitField Bitfield
        {
            get { return bitfield; }
        }

        public InfoHash Infohash
        {
            get { return infoHash; }
        }

        public List<Peer> ActivePeers
        {
            get { return activePeers; }
        }

        public List<Peer> AvailablePeers
        {
            get { return availablePeers; }
        }

        public List<Peer> BannedPeers
        {
            get { return bannedPeers; }
        }

        public List<Peer> BusyPeers
        {
            get { return busyPeers; }
        }

        public FastResume()
        {
        }

        public FastResume(InfoHash infoHash, BitField bitfield, List<Peer> activePeers, List<Peer> availablePeers, List<Peer> bannedPeers, List<Peer> busyPeers)
        {
            if (infoHash==null)
                throw new ArgumentNullException("infoHash");

            if(bitfield == null)
                throw new ArgumentNullException("bitfield");

            this.infoHash = infoHash;
            this.bitfield = bitfield;

            this.activePeers    = activePeers;
            this.availablePeers = availablePeers;
            this.bannedPeers    = bannedPeers;
            this.busyPeers      = busyPeers;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckContent(dict, VersionKey, (BEncodedNumber)2);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            CheckContent(dict, ActivePeersKey);
            CheckContent(dict, AvailablePeersKey);
            CheckContent(dict, BannedPeersKey);
            CheckContent(dict, BusyPeersKey);

            infoHash = new InfoHash(((BEncodedString)dict[InfoHashKey]).TextBytes);
            bitfield = new BitField((int)((BEncodedNumber)dict[BitfieldLengthKey]).Number);
            byte[] data = ((BEncodedString)dict[BitfieldKey]).TextBytes;
            bitfield.FromArray(data, 0, data.Length);

            activePeers    = Peer.Decode((BEncodedList)dict[ActivePeersKey]);
            availablePeers = Peer.Decode((BEncodedList)dict[AvailablePeersKey]);
            bannedPeers    = Peer.Decode((BEncodedList)dict[BannedPeersKey]);
            busyPeers      = Peer.Decode((BEncodedList)dict[BusyPeersKey]);
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
            dict.Add(VersionKey, (BEncodedNumber)2);
            dict.Add(InfoHashKey, new BEncodedString(infoHash.Hash));
            dict.Add(BitfieldKey, new BEncodedString(bitfield.ToByteArray()));
            dict.Add(BitfieldLengthKey, (BEncodedNumber)bitfield.Length);

            dict.Add(ActivePeersKey,    new BEncodedList(Peer.Encode(activePeers)));
            dict.Add(AvailablePeersKey, new BEncodedList(Peer.Encode(availablePeers)));
            dict.Add(BannedPeersKey,    new BEncodedList(Peer.Encode(bannedPeers)));
            dict.Add(BusyPeersKey,      new BEncodedList(Peer.Encode(busyPeers)));

            return dict;
        }

        public void Encode(Stream s)
        {
            byte[] data = Encode().Encode();
            s.Write(data, 0, data.Length);
        }
    }
}
