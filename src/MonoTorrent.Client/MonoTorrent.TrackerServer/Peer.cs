//
// Peer.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
using System.Net;

using MonoTorrent.BEncoding;

namespace MonoTorrent.TrackerServer
{
    ///<summary>This class holds informations about Peers downloading Files</summary>
    public class Peer : IEquatable<Peer>
    {
        internal Peer (IPEndPoint endPoint, object dictionaryKey)
        {
            ClientAddress = endPoint;
            DictionaryKey = dictionaryKey;
        }

        internal Peer (AnnounceRequest par, object dictionaryKey)
        {
            DictionaryKey = dictionaryKey;
            Update (par);
        }


        /// <summary>
        /// The IPEndpoint at which the client is listening for connections at
        /// </summary>
        public IPEndPoint ClientAddress { get; private set; }

        public Software ClientApp { get; private set; }

        ///<summary>
        /// A byte[] containing the peer's IPEndpoint in compact form
        ///</summary>
        internal byte[] CompactEntry => GenerateCompactPeersEntry ();

        internal object DictionaryKey { get; }

        /// <summary>
        /// The amount of data (in bytes) which the peer has downloaded this session
        /// </summary>
        public long Downloaded { get; set; }

        /// <summary>
        /// The estimated download speed of the peer in bytes/second
        /// </summary>
        public int DownloadSpeed { get; set; }

        ///<summary>
        /// True if the peer has completed the torrent
        /// </summary>
        public bool HasCompleted => Remaining == 0;

        /// <summary>
        /// The time when the peer last announced at
        /// </summary>
        public DateTime LastAnnounceTime { get; set; }

        ///<summary>The peer entry in non compact format.</summary> 
        internal BEncodedDictionary NonCompactEntry => GeneratePeersEntry ();

        ///<summary>
        ///The Id of the client software
        ///</summary>
        public BEncodedString PeerId { get; set; }

        /// <summary>
        /// The amount of data (in bytes) which the peer has to download to complete the torrent
        /// </summary>
        public long Remaining { get; set; }

        /// <summary>
        /// The amount of data the peer has uploaded this session
        /// </summary>
        public long Uploaded { get; set; }

        /// <summary>
        /// The estimated upload speed of the peer in bytes/second
        /// </summary>
        public int UploadSpeed { get; set; }

        public override bool Equals (object obj)
        {
            return Equals (obj as Peer);
        }

        public bool Equals (Peer other)
        {
            if (other == null)
                return false;
            return DictionaryKey.Equals (other.DictionaryKey);
        }

        public override int GetHashCode ()
        {
            return DictionaryKey.GetHashCode ();
        }

        internal void Update (AnnounceRequest parameters)
        {
            DateTime now = DateTime.Now;
            double elapsedTime = (now - LastAnnounceTime).TotalSeconds;
            if (elapsedTime < 1)
                elapsedTime = 1;

            ClientAddress = parameters.ClientAddress;
            DownloadSpeed = (int) ((parameters.Downloaded - Downloaded) / elapsedTime);
            UploadSpeed = (int) ((parameters.Uploaded - Uploaded) / elapsedTime);
            Downloaded = parameters.Downloaded;
            Uploaded = parameters.Uploaded;
            Remaining = parameters.Left;
            PeerId = parameters.PeerId;
            ClientApp = new Software (parameters.PeerId);
            LastAnnounceTime = now;
        }


        BEncodedDictionary GeneratePeersEntry ()
        {
            BEncodedString encPeerId = PeerId;
            var encAddress = new BEncodedString (ClientAddress.Address.ToString ());
            var encPort = new BEncodedNumber (ClientAddress.Port);

            var dictionary = new BEncodedDictionary {
                { TrackerServer.PeerIdKey, encPeerId },
                { TrackerServer.Ip, encAddress },
                { TrackerServer.Port, encPort }
            };
            return dictionary;
        }
        byte[] GenerateCompactPeersEntry ()
        {
            byte[] port = BitConverter.GetBytes (IPAddress.HostToNetworkOrder ((short) ClientAddress.Port));
            byte[] addr = ClientAddress.Address.GetAddressBytes ();
            byte[] entry = new byte[addr.Length + port.Length];

            Array.Copy (addr, entry, addr.Length);
            Array.Copy (port, 0, entry, addr.Length, port.Length);
            return entry;
        }
    }
}
