//
// Peer.cs
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
using System.Net;
using System.Text;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    public class Peer
    {
        /// <summary>
        /// The number of times this peer has had it's connection closed
        /// </summary>
        internal int CleanedUpCount { get; set; }

        /// <summary>
        /// The URI used to make an outgoing connection to this peer.
        /// </summary>
        public Uri ConnectionUri { get; }

        /// <summary>
        /// The types of encryption the peer supports. Initially this is 'All', and we remove 'RC4' and 'RC4' header from
        /// the list if we fail to establish an encrypted connection, so the next time we try to connect we use plain text.
        /// </summary>
        public EncryptionTypes Encryption { get; internal set; }

        /// <summary>
        /// The number of times we failed to establish an outgoing connection to this peer.
        /// </summary>
        internal int FailedConnectionAttempts { get; set; }

        /// <summary>
        /// A cache of the last known seeding state of this peer. This is used to avoid connecting to seeders when 100%
        /// of the torrent has been downloaded.
        /// </summary>
        internal bool IsSeeder { get; set; }

        /// <summary>
        /// The local port this peer is listening for connections on.
        /// </summary>
        internal int LocalPort { get; set; }

        /// <summary>
        /// The 20 byte identifier for the peer.
        /// </summary>
        internal BEncodedString PeerId { get; set; }

        /// <summary>
        /// The number of times, in a row, that this peer has sent us the blocks for a piece and that
        /// piece failed the hash check.
        /// </summary>
        internal int RepeatedHashFails { get; set; }

        /// <summary>
        /// This is the overall count for the number of pieces which failed the hash check after being
        /// received from this peer.
        /// </summary>
        internal int TotalHashFails { get; set; }


        public Peer(BEncodedString peerId, Uri connectionUri)
            : this (peerId, connectionUri, EncryptionTypes.All)
        {

        }

        public Peer(BEncodedString peerId, Uri connectionUri, EncryptionTypes encryption)
        {
            PeerId = peerId ?? throw new ArgumentNullException (nameof (peerId));
            ConnectionUri = connectionUri ?? throw new ArgumentNullException(nameof (connectionUri));
            Encryption = encryption;
        }

        public override bool Equals(object obj)
            => Equals (obj as Peer);

        public bool Equals(Peer other)
        {
            if (other == null)
                return false;

            // FIXME: Don't compare the port, just compare the IP
            if (BEncodedString.IsNullOrEmpty (PeerId) || BEncodedString.IsNullOrEmpty(other.PeerId))
                return ConnectionUri.Host.Equals(other.ConnectionUri.Host);

            return PeerId.Equals (other.PeerId);
        }

        public override int GetHashCode()
            =>ConnectionUri.Host.GetHashCode();

        internal byte[] CompactPeer()
        {
            byte[] data = new byte[6];
            CompactPeer(data, 0);
            return data;
        }

        internal void CompactPeer(byte[] data, int offset)
        {
            Buffer.BlockCopy(IPAddress.Parse(ConnectionUri.Host).GetAddressBytes(), 0, data, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(((short)ConnectionUri.Port))), 0, data, offset + 4, 2);
        }

        internal void HashedPiece(bool succeeded)
        {
            if (succeeded && RepeatedHashFails > 0)
                RepeatedHashFails--;
            
            if (!succeeded)
            {
                RepeatedHashFails++;
                TotalHashFails++;
            }
        }

        public override string ToString()
            => ConnectionUri.ToString();

        public static IList<Peer> Decode(BEncodedList peers)
        {
            MonoTorrentCollection<Peer> list = new MonoTorrentCollection<Peer>(peers.Count);
            foreach (BEncodedValue value in peers)
            {
                try
                {
                    if (value is BEncodedDictionary)
                        list.Add(DecodeFromDict((BEncodedDictionary)value));
                    else if (value is BEncodedString)
                        foreach (Peer p in Decode((BEncodedString)value))
                            list.Add(p);
                }
                catch
                {
                    // If something is invalid and throws an exception, ignore it
                    // and continue decoding the rest of the peers
                }
            }
            return list;
        }

        private static Peer DecodeFromDict(BEncodedDictionary dict)
        {
            BEncodedString peerId;

            if (dict.ContainsKey("peer id"))
                peerId = (BEncodedString) dict["peer id"];
            else if (dict.ContainsKey("peer_id"))       // HACK: Some trackers return "peer_id" instead of "peer id"
                peerId = (BEncodedString) dict["peer_id"];
            else
                peerId = BEncodedString.Empty;

            Uri connectionUri = new Uri("ipv4://" + dict["ip"].ToString() + ":" + dict["port"].ToString());
            return new Peer(peerId, connectionUri, EncryptionTypes.All);
        }

        public static IList<Peer> Decode(BEncodedString peers)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes
            byte[] byteOrderedData = peers.TextBytes;
            int i = 0;
            UInt16 port;
            StringBuilder sb = new StringBuilder(27);
            MonoTorrentCollection<Peer> list = new MonoTorrentCollection<Peer>((byteOrderedData.Length / 6) + 1);
            while ((i + 5) < byteOrderedData.Length)
            {
                sb.Remove(0, sb.Length);

                sb.Append("ipv4://");
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);

                port = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(byteOrderedData, i));
                i += 2;
                sb.Append(':');
                sb.Append(port);

                Uri uri = new Uri(sb.ToString());
                list.Add(new Peer("", uri, EncryptionTypes.All));
            }

            return list;
        }

        internal static BEncodedList Encode(IEnumerable<Peer> peers)
        {
            BEncodedList list = new BEncodedList();
            foreach (Peer p in peers)
                list.Add((BEncodedString)p.CompactPeer());
            return list;
        }
    }
}