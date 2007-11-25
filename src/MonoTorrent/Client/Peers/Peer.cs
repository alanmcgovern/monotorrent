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
using System.Text;
using System.Net;
using MonoTorrent.Common;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    internal class Peer
    {
        #region Private Fields

        private bool activeReceive;
        private bool activeSend;
        private int cleanedUpCount;
        private PeerConnectionBase connection;
        private EncryptionMethods encryptionSupported = EncryptionMethods.RC4Encryption;
        private int failedConnectionAttempts;
        private int totalHashFails;
        private bool isSeeder;
        private string location;
        private string peerId;
        private int repeatedHashFails;
        private DateTime lastConnectionAttempt;

        #endregion Private Fields


        #region Properties

        internal bool ActiveReceive
        {
            get { return this.activeReceive; }
            set { this.activeReceive = value; }
        }

        internal bool ActiveSend
        {
            get { return this.activeSend; }
            set { this.activeSend = value; }
        }

        /// <summary>
        /// The connection associated with this peer
        /// </summary>
        public PeerConnectionBase Connection
        {
            get { return this.connection; }
            set { this.connection = value; }
        }


        public int CleanedUpCount
        {
            get { return this.cleanedUpCount; }
            set { this.cleanedUpCount = value; }
        }


        /// <summary>
        /// Returns the number of times the peer has sent us a piece which failed a hashcheck
        /// </summary>
        public int TotalHashFails
        {
            get { return this.totalHashFails; }
        }


        /// <summary>
        /// The ID of the peer
        /// </summary>
        public string PeerId
        {
            get { return peerId; }
            internal set { peerId = value; }
        }


        /// <summary>
        /// True if the peer is a seeder
        /// </summary>
        public bool IsSeeder
        {
            get { return this.isSeeder; }
            internal set { this.isSeeder = value; }
        }


        /// <summary>
        /// The number of times we tried to connect to the peer and failed
        /// </summary>
        public int FailedConnectionAttempts
        {
            get { return this.failedConnectionAttempts; }
            internal set { this.failedConnectionAttempts = value; }
        }


        internal DateTime LastConnectionAttempt
        {
            get { return this.lastConnectionAttempt; }
            set { this.lastConnectionAttempt = value; }
        }


        /// <summary>
        /// The location at which the peer can be connected to at
        /// </summary>
        public string Location
        {
            get { return this.location; }
        }


        /// <summary>
        /// The highest level of encryption that should be attempted with this peer
        /// </summary>
        public EncryptionMethods EncryptionSupported
        {
            get { return this.encryptionSupported; }
            internal set { this.encryptionSupported = value; }
        }

        public int RepeatedHashFails
        {
            get { return this.repeatedHashFails; }
        }

        #endregion Properties


        #region Constructors
        public Peer(string peerId, string location)
        {
            this.peerId = peerId;
            this.location = location;
        }
        #endregion


        public override bool Equals(object obj)
        {
            Peer peer = obj as Peer;
            if(peer ==null)
                return false;

            return this.location.Equals(peer.location);
        }


        public override int GetHashCode()
        {
            return this.location.GetHashCode();
        }


        public override string ToString()
        {
            return this.location;
        }


        public byte[] CompactPeer()
        {
            byte[] data = new byte[6];

            string[] peer = this.location.Split(':');
            Buffer.BlockCopy(IPAddress.Parse(peer[0]).GetAddressBytes(), 0, data, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(short.Parse(peer[1]))), 0, data, 4, 2);

            return data;
        }


        internal void HashedPiece(bool succeeded)
        {
            if (succeeded && repeatedHashFails > 0)
                repeatedHashFails--;
            
            if (!succeeded)
            {
                repeatedHashFails++;
                totalHashFails++;
            }
        }


        public static MonoTorrentCollection<Peer> Decode(BEncodedList peers)
        {
            MonoTorrentCollection<Peer> list = new MonoTorrentCollection<Peer>(peers.Count);
            foreach (BEncodedDictionary dict in peers)
            {
                string peerId;

                if (dict.ContainsKey("peer id"))
                    peerId = dict["peer id"].ToString();
                else if (dict.ContainsKey("peer_id"))       // HACK: Some trackers return "peer_id" instead of "peer id"
                    peerId = dict["peer_id"].ToString();
                else
                    peerId = string.Empty;

                list.Add(new Peer(peerId, dict["ip"].ToString() + ':' + dict["port"].ToString()));
            }

            return list;
        }

        public static MonoTorrentCollection<Peer> Decode(BEncodedString peers)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes
            byte[] byteOrderedData = peers.TextBytes;
            int i = 0;
            UInt16 port;
            Peer peer;
            StringBuilder sb = new StringBuilder(16);
            MonoTorrentCollection<Peer> list = new MonoTorrentCollection<Peer>((byteOrderedData.Length / 6) + 1);
            while (i < byteOrderedData.Length)
            {
                sb.Remove(0, sb.Length);

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
                list.Add(new Peer("", sb.ToString()));
            }

            return list;
        }
    }
}