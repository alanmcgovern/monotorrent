//
// FastResume.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.IO;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    public class FastResume
    {
        // Version 1 stored the Bitfield and Infohash.
        //
        // Version 2 added the UnhashedPieces bitfield.
        //
        static readonly BEncodedNumber FastResumeVersion = 3;

        internal static readonly BEncodedString InfoHashKey = "infohash";
        internal static readonly BEncodedString UnhashedPiecesKey = "unhashed_pieces";

        internal static readonly BEncodedString BitfieldKey = "bitfield";
        internal static readonly BEncodedString BitfieldLengthKey = "bitfield_length";

        internal static readonly BEncodedString VersionKey = "version";

        private static readonly BEncodedString ActivePeersKey         = (BEncodedString)"ActivePeers";
        //private static readonly BEncodedString ActivePeers_LengthKey  = (BEncodedString)"ActivePeers_Length";
        private static readonly BEncodedString AvailablePeersKey      = (BEncodedString)"AvailablePeers";
        //private static readonly BEncodedString AvailablePeers_LengthKey = (BEncodedString)"AvailablePeers_Length";
        private static readonly BEncodedString BannedPeersKey         = (BEncodedString)"BannedPeers";
        //private static readonly BEncodedString BannedPeers_LengthKey  = (BEncodedString)"BannedPeers_Length";
        private static readonly BEncodedString BusyPeersKey           = (BEncodedString)"BusyPeers";
        //private static readonly BEncodedString BusyPeers_LengthKey    = (BEncodedString)"BusyPeers_Length";


        public BitField Bitfield { get; }
        public InfoHash Infohash { get; }

        public BitField UnhashedPieces { get; }

        private IList<Peer> activePeers;
        private IList<Peer> availablePeers;
        private IList<Peer> bannedPeers;
        private IList<Peer> busyPeers;


        [Obsolete ("This constructor should not be used")]
        public FastResume ()
        {
        }

        public IList<Peer> ActivePeers
        {
            get { return activePeers; }
        }

        public IList<Peer> AvailablePeers
        {
            get { return availablePeers; }
        }

        public IList<Peer> BannedPeers
        {
            get { return bannedPeers; }
        }

        public IList<Peer> BusyPeers
        {
            get { return busyPeers; }
        }

        [Obsolete("The constructor overload which takes an 'unhashedPieces' parameter should be used instead of this.")]
        public FastResume(InfoHash infoHash, BitField bitfield)
        {
            Infohash = infoHash ?? throw new ArgumentNullException (nameof (infoHash));
            Bitfield = bitfield ?? throw new ArgumentNullException (nameof (bitfield));
            UnhashedPieces = new BitField (Bitfield.Length);
        }

        [Obsolete("The constructor overload which takes an 'unhashedPieces' parameter should be used instead of this.")]
        public FastResume(InfoHash infoHash, BitField bitfield, List<Peer> activePeers, List<Peer> availablePeers, List<Peer> bannedPeers, List<Peer> busyPeers)
        {
            Infohash = infoHash ?? throw new ArgumentNullException (nameof (infoHash));
            Bitfield = bitfield?.Clone () ?? throw new ArgumentNullException (nameof (bitfield));

            if (infoHash==null)
                throw new ArgumentNullException("infoHash");

            if(bitfield == null)
                throw new ArgumentNullException("bitfield");

            this.activePeers    = activePeers;
            this.availablePeers = availablePeers;
            this.bannedPeers    = bannedPeers;
            this.busyPeers      = busyPeers;
        }

        public FastResume(InfoHash infoHash, BitField bitfield, BitField unhashedPieces, List<Peer> activePeers, List<Peer> availablePeers, List<Peer> bannedPeers, List<Peer> busyPeers)
        {
            Infohash = infoHash ?? throw new ArgumentNullException(nameof(infoHash));
            Bitfield = bitfield?.Clone() ?? throw new ArgumentNullException(nameof(bitfield));
            UnhashedPieces = unhashedPieces?.Clone() ?? throw new ArgumentNullException(nameof(UnhashedPieces));

            if (infoHash == null)
                throw new ArgumentNullException("infoHash");

            if (bitfield == null)
                throw new ArgumentNullException("bitfield");

            for (int i = 0; i < Bitfield.Length; i++)
            {
                if (bitfield[i] && unhashedPieces[i])
                    throw new ArgumentException($"The bitfield is set to true at index {i} but that piece is marked as unhashed.");
            }

            this.activePeers = activePeers;
            this.availablePeers = availablePeers;
            this.bannedPeers = bannedPeers;
            this.busyPeers = busyPeers;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckVersion(dict);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            CheckContent(dict, ActivePeersKey);
            CheckContent(dict, AvailablePeersKey);
            CheckContent(dict, BannedPeersKey);
            CheckContent(dict, BusyPeersKey);

            Infohash = new InfoHash(((BEncodedString)dict[InfoHashKey]).TextBytes);

            Bitfield = new BitField((int)((BEncodedNumber)dict[BitfieldLengthKey]).Number);
            byte[] data = ((BEncodedString)dict[BitfieldKey]).TextBytes;
            Bitfield.FromArray(data, 0, data.Length);

            UnhashedPieces = new BitField (Bitfield.Length);
            // If we're loading up an older version of the FastResume data then we
            if (dict.ContainsKey (UnhashedPiecesKey)) {
                data = ((BEncodedString)dict[UnhashedPiecesKey]).TextBytes;
                UnhashedPieces.FromArray(data, 0, data.Length);
            }

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

        private void CheckVersion (BEncodedDictionary dict)
        {
            var version = (dict [VersionKey] as BEncodedNumber)?.Number;

            if (version.GetValueOrDefault () == 1 || version.GetValueOrDefault () == 2 || version.GetValueOrDefault() == 3)
                return;

            throw new ArgumentException ($"This FastResume is version {version}, but only version  '1' and '2' and '3' are supported");
        }

        public BEncodedDictionary Encode()
        {

            BEncodedDictionary dict = new BEncodedDictionary();

            dict.Add(VersionKey, (BEncodedNumber)3);
            dict.Add(InfoHashKey, new BEncodedString(Infohash.Hash));
            dict.Add(BitfieldKey, new BEncodedString(Bitfield.ToByteArray()));
            dict.Add(BitfieldLengthKey, (BEncodedNumber)Bitfield.Length);
            dict.Add(UnhashedPiecesKey, new BEncodedString (UnhashedPieces.ToByteArray ()) );


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
