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
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using MonoTorrent.Common;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker
{
    ///<summary>This class holds informations about Peers downloading Files</summary>
    public class Peer
    {
        private IPEndPoint clientAddress;
        private long downloaded;
        private long uploaded;
        private long left;
        private int downloadSpeed;
        private int uploadSpeed;
        private DateTime lastAnnounceTime;
        private string peerId;


        internal Peer(AnnounceParameters par)
        {
            Update(par);
        }


        /// <summary>
        /// The IPEndpoint at which the client is listening for connections at
        /// </summary>
        public IPEndPoint ClientAddress
        {
            get { return clientAddress; }
        }

        ///<summary>
        /// A byte[] containing the peer's IPEndpoint in compact form
        ///</summary>
        internal byte[] CompactEntry
        {
            get
            {
                // This is 100% threadsafe - worst case scenario means we generate the listing N times and GC N-1 of them
                //if (compactEntry == null)
                    return GenerateCompactPeersEntry();
                //return compactEntry;
            }
        }

        /// <summary>
        /// The amount of data (in bytes) which the peer has downloaded this session
        /// </summary>
        public long Downloaded
        {
            get { return downloaded; }
        }

        /// <summary>
        /// The estimated download speed of the peer in bytes/second
        /// </summary>
        public int DownloadSpeed
        {
            get { return downloadSpeed; }
        }

        ///<summary>
        /// True if the peer has completed the torrent
        /// </summary>
        public bool HasCompleted
        {
            get { return Remaining == 0; }
        }

        /// <summary>
        /// The time when the peer last announced at
        /// </summary>
        public DateTime LastAnnounceTime
        {
            get { return lastAnnounceTime; }
        }

        ///<summary>The peer entry in non compact format.</summary> 
        internal BEncodedDictionary NonCompactEntry
        {
            get
            {
                // This is 100% threadsafe - worst case scenario means we generate the listing N times and GC N-1 of them
                //if (noncompactEntry == null)
                    return GeneratePeersEntry();
                //return noncompactEntry;
            }
        }

        ///<summary>
        ///The Id of the client software
        ///</summary>
        public string PeerId
        {
            get { return peerId; }
        }

        /// <summary>
        /// The amount of data (in bytes) which the peer has to download to complete the torrent
        /// </summary>
        public long Remaining
        {
            get { return left; }
        }

        /// <summary>
        /// The amount of data the peer has uploaded this session
        /// </summary>
        public long Uploaded
        {
            get { return uploaded; }
        }

        /// <summary>
        /// The estimated upload speed of the peer in bytes/second
        /// </summary>
        public int UploadSpeed
        {
            get { return uploadSpeed; }
        }


        ///<summary>Update internal datas and reset Timers</summary>
        internal void Update(AnnounceParameters parameters)
        {
            DateTime now = DateTime.Now;
            double elapsedTime = (now - lastAnnounceTime).TotalSeconds;
            if (elapsedTime < 1)
                elapsedTime = 1;

            clientAddress = parameters.ClientAddress;
            downloaded = parameters.Downloaded;
            uploaded = parameters.Uploaded;
            left = parameters.Left;
            peerId = parameters.PeerId;
            downloadSpeed = (int)((parameters.Downloaded - parameters.Downloaded) / elapsedTime);
            lastAnnounceTime = now;
            uploadSpeed = (int)((parameters.Uploaded - parameters.Uploaded) / elapsedTime);
        }


        private BEncodedDictionary GeneratePeersEntry()
        {
            BEncodedString encPeerId = new BEncodedString(PeerId);
            BEncodedString encAddress = new BEncodedString(ClientAddress.Address.ToString());
            BEncodedNumber encPort = new BEncodedNumber(ClientAddress.Port);

            BEncodedDictionary dictionary = new BEncodedDictionary();
            dictionary.Add(Tracker.peer_id, encPeerId);
            dictionary.Add(Tracker.ip, encAddress);
            dictionary.Add(Tracker.port, encPort);
            return dictionary;
            //noncompactEntry = dictionary;
        }
        private byte[] GenerateCompactPeersEntry()
        {
            byte[] port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)ClientAddress.Port));
            byte[] addr = ClientAddress.Address.GetAddressBytes();
            byte[] entry = new byte[addr.Length + port.Length];

            Array.Copy(addr, entry, addr.Length);
            Array.Copy(port, 0, entry, addr.Length, port.Length);
            return entry;
            //compactEntry = entry;
        }
    }
}