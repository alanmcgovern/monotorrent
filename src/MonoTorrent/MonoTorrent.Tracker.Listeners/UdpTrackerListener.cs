﻿//
// UdpTrackerListener.cs
//
// Authors:
//   olivier Dufour olivier(dot)duff(at)gmail.com
//
// Copyright (C) 2006 olivier Dufour
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
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.Messages.UdpTracker;

namespace MonoTorrent.Tracker.Listeners
{
    class UdpTrackerListener : TrackerListener, ISocketListener
    {
        public IPEndPoint EndPoint { get; private set; }

        IPEndPoint OriginalEndPoint { get; }

        Dictionary<IPAddress, long> ConnectionIDs { get; }
        long curConnectionID;

        public UdpTrackerListener (int port)
            : this (new IPEndPoint (IPAddress.Any, port))
        {
        }

        public UdpTrackerListener (IPEndPoint endPoint)
        {
            ConnectionIDs = new Dictionary<IPAddress, long> ();
            EndPoint = OriginalEndPoint = endPoint;
        }

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        protected override void Start (CancellationToken token)
        {
            var listener = new UdpClient (OriginalEndPoint);
            token.Register (() => listener.Dispose ());

            EndPoint = (IPEndPoint) listener.Client.LocalEndPoint;

            ReceiveAsync (listener, token);
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            Task sendTask = null;
            while (!token.IsCancellationRequested) {
                try {
                    var result = await client.ReceiveAsync ();
                    var data = result.Buffer;
                    if (data.Length < 16)
                        return;//bad request

                    var request = UdpTrackerMessage.DecodeMessage (data, 0, data.Length, MessageType.Request);

                    if (sendTask != null) {
                        try {
                            await sendTask;
                        } catch {

                        }
                    }


                    switch (request.Action) {
                        case 0:
                            sendTask = ReceiveConnect (client, (ConnectMessage) request, result.RemoteEndPoint);
                            break;
                        case 1:
                            sendTask = ReceiveAnnounce (client, (AnnounceMessage) request, result.RemoteEndPoint);
                            break;
                        case 2:
                            sendTask = ReceiveScrape (client, (ScrapeMessage) request, result.RemoteEndPoint);
                            break;
                        case 3:
                            sendTask = ReceiveError (client, (ErrorMessage) request, result.RemoteEndPoint);
                            break;
                        default:
                            throw new ProtocolException ($"Invalid udp message received: {request.Action}");
                    }
                } catch (Exception e) {
                    Logger.Log (null, e.ToString ());
                }
            }
        }

        protected virtual async Task ReceiveConnect (UdpClient client, ConnectMessage connectMessage, IPEndPoint remotePeer)
        {
            UdpTrackerMessage m;
            if (connectMessage.ConnectionId == ConnectMessage.InitialiseConnectionId)
                m = new ConnectResponseMessage (connectMessage.TransactionId, CreateConnectionID (remotePeer));
            else
                m = new ErrorMessage (connectMessage.TransactionId, $"The connection_id was {connectMessage.ConnectionId} but expected {ConnectMessage.InitialiseConnectionId}");

            var data = m.Encode ();
            try {
                await client.SendAsync (data, data.Length, remotePeer);
            } catch {
            }
        }

        //TODO is endpoint.Address.Address enough and do we really need this complex system for connection ID
        //advantage: this system know if we have ever connect before announce scrape request...
        long CreateConnectionID (IPEndPoint remotePeer)
        {
            curConnectionID++;
            if (!ConnectionIDs.ContainsKey (remotePeer.Address))
                ConnectionIDs.Add (remotePeer.Address, curConnectionID);
            return curConnectionID;
        }

        //QUICKHACK: format bencoded val and get it back wereas must refactor tracker system to have more generic object...
        protected virtual async Task ReceiveAnnounce (UdpClient client, AnnounceMessage announceMessage, IPEndPoint remotePeer)
        {
            UdpTrackerMessage m;
            var dict = Handle (getCollection (announceMessage), remotePeer.Address, false);
            if (dict.ContainsKey (TrackerRequest.FailureKey)) {
                m = new ErrorMessage (announceMessage.TransactionId, dict[TrackerRequest.FailureKey].ToString ());
            } else {
                var interval = TimeSpan.Zero;
                var leechers = 0;
                var seeders = 0;
                var peers = new List<MonoTorrent.Client.Peer> ();
                foreach (var keypair in dict) {
                    switch (keypair.Key.Text) {
                        case ("complete"):
                            seeders = Convert.ToInt32 (keypair.Value.ToString ());//same as seeder?
                            break;

                        case ("incomplete"):
                            leechers = Convert.ToInt32 (keypair.Value.ToString ());//same as leecher?
                            break;

                        case ("interval"):
                            interval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()));
                            break;

                        case ("peers"):
                            if (keypair.Value is BEncodedList)          // Non-compact response
                                peers.AddRange (MonoTorrent.Client.Peer.Decode ((BEncodedList) keypair.Value));
                            else if (keypair.Value is BEncodedString)   // Compact response
                                peers.AddRange (MonoTorrent.Client.Peer.Decode ((BEncodedString) keypair.Value));
                            break;

                        default:
                            break;
                    }
                }
                m = new AnnounceResponseMessage (announceMessage.TransactionId, interval, leechers, seeders, peers);
            }
            var data = m.Encode ();
            await client.SendAsync (data, data.Length, remotePeer);
        }

        NameValueCollection getCollection (AnnounceMessage announceMessage)
        {
            var res = new NameValueCollection ();
            res.Add ("info_hash", announceMessage.InfoHash.UrlEncode ());
            res.Add ("peer_id", announceMessage.PeerId.UrlEncode ());
            res.Add ("port", announceMessage.Port.ToString ());
            res.Add ("uploaded", announceMessage.Uploaded.ToString ());
            res.Add ("downloaded", announceMessage.Downloaded.ToString ());
            res.Add ("left", announceMessage.Left.ToString ());
            res.Add ("compact", "1");//hardcode
            res.Add ("numwant", announceMessage.NumWanted.ToString ());
            res.Add ("ip", announceMessage.IP.ToString ());
            res.Add ("key", announceMessage.Key.ToString ());
            res.Add ("event", announceMessage.TorrentEvent.ToString ().ToLower ());
            return res;
        }

        protected virtual async Task ReceiveScrape (UdpClient client, ScrapeMessage scrapeMessage, IPEndPoint remotePeer)
        {
            var val = Handle (getCollection (scrapeMessage), remotePeer.Address, true);

            UdpTrackerMessage m;
            byte[] data;
            if (val.ContainsKey (TrackerRequest.FailureKey)) {
                m = new ErrorMessage (scrapeMessage.TransactionId, val[TrackerRequest.FailureKey].ToString ());
            } else {
                var scrapes = new List<ScrapeDetails> ();

                foreach (var keypair in val) {
                    var dict = (BEncodedDictionary) keypair.Value;
                    var seeds = 0;
                    var leeches = 0;
                    var complete = 0;
                    foreach (var keypair2 in dict) {
                        switch (keypair2.Key.Text) {
                            case "complete"://The current number of connected seeds
                                seeds = Convert.ToInt32 (keypair2.Value.ToString ());
                                break;
                            case "downloaded"://The total number of completed downloads
                                complete = Convert.ToInt32 (keypair2.Value.ToString ());
                                break;
                            case "incomplete":
                                leeches = Convert.ToInt32 (keypair2.Value.ToString ());
                                break;
                        }
                    }
                    var sd = new ScrapeDetails (seeds, leeches, complete);
                    scrapes.Add (sd);
                    if (scrapes.Count == 74)//protocole do not support to send more than 74 scrape at once...
                    {
                        m = new ScrapeResponseMessage (scrapeMessage.TransactionId, scrapes);
                        data = m.Encode ();
                        await client.SendAsync (data, data.Length, remotePeer);
                        scrapes.Clear ();
                    }
                }
                m = new ScrapeResponseMessage (scrapeMessage.TransactionId, scrapes);
            }
            data = m.Encode ();
            await client.SendAsync (data, data.Length, remotePeer);
        }

        NameValueCollection getCollection (ScrapeMessage scrapeMessage)
        {
            var res = new NameValueCollection ();
            if (scrapeMessage.InfoHashes.Count == 0)
                return res;//no infohash????
            //TODO more than one infohash : paid attention to order in response!!!
            var hash = new InfoHash (scrapeMessage.InfoHashes[0]);
            res.Add ("info_hash", hash.UrlEncode ());
            return res;
        }

        protected virtual Task ReceiveError (UdpClient client, ErrorMessage errorMessage, IPEndPoint remotePeer)
        {
            throw new ProtocolException ($"ErrorMessage from :{remotePeer.Address}");
        }
    }
}
