//
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
using MonoTorrent.Logging;
using MonoTorrent.Messages.UdpTracker;
using MonoTorrent.Trackers;
using MonoTorrent.TrackerServer;

namespace MonoTorrent.Connections.TrackerServer
{
    public class UdpTrackerListener : TrackerListener, ISocketListener
    {
        static readonly Logger logger = Logger.Create (nameof (UdpTrackerListener));

        public IPEndPoint? LocalEndPoint { get; private set; }

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
            OriginalEndPoint = endPoint;
        }

        /// <summary>
        /// Starts listening for incoming connections
        /// </summary>
        protected override void StartCore (CancellationToken token)
        {
            var listener = new UdpClient (OriginalEndPoint);
            token.Register (() => {
                LocalEndPoint = null;
                listener.Dispose ();
            });

            LocalEndPoint = (IPEndPoint?) listener.Client.LocalEndPoint;

            ReceiveAsync (listener, token);
            RaiseStatusChanged (ListenerStatus.Listening);
        }

        async void ReceiveAsync (UdpClient client, CancellationToken token)
        {
            Task? sendTask = null;
            while (!token.IsCancellationRequested) {
                try {
                    UdpReceiveResult result = await client.ReceiveAsync ();
                    byte[] data = result.Buffer;
                    if (data.Length < 16)
                        return; //bad request

                    var request = UdpTrackerMessage.DecodeMessage (data.AsSpan (0, data.Length), MessageType.Request, result.RemoteEndPoint.AddressFamily);

                    if (sendTask != null) {
                        try {
                            await sendTask;
                        } catch {

                        }
                    }

                    sendTask = request.Action switch {
                        0 => ReceiveConnect (client, (ConnectMessage) request, result.RemoteEndPoint),
                        1 => ReceiveAnnounce (client, (AnnounceMessage) request, result.RemoteEndPoint),
                        2 => ReceiveScrape (client, (ScrapeMessage) request, result.RemoteEndPoint),
                        3 => ReceiveError (client, (ErrorMessage) request, result.RemoteEndPoint),
                        _ => throw new InvalidOperationException ($"Invalid udp message received: {request.Action}")
                    };
                } catch (Exception e) {
                    logger.Exception (e, "Exception while receiving a message");
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

            ReadOnlyMemory<byte> data = m.Encode ();
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
            BEncodedDictionary dict = Handle (getCollection (announceMessage), remotePeer.Address, false);
            if (dict.ContainsKey (TrackerRequest.FailureKey)) {
                m = new ErrorMessage (announceMessage.TransactionId, dict[TrackerRequest.FailureKey].ToString ()!);
            } else {
                TimeSpan interval = TimeSpan.Zero;
                int leechers = 0;
                int seeders = 0;
                var peers = new List<PeerInfo> ();
                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                    switch (keypair.Key.Text) {
                        case ("complete"):
                            seeders = Convert.ToInt32 (keypair.Value.ToString ());//same as seeder?
                            break;

                        case ("incomplete"):
                            leechers = Convert.ToInt32 (keypair.Value.ToString ());//same as leecher?
                            break;

                        case ("interval"):
                            interval = TimeSpan.FromSeconds (int.Parse (keypair.Value.ToString ()!));
                            break;

                        case ("peers"):
                            if (keypair.Value is BEncodedList)          // Non-compact response
                                peers.AddRange (PeerDecoder.Decode ((BEncodedList) keypair.Value, remotePeer.AddressFamily));
                            else if (keypair.Value is BEncodedString str)   // Compact response
                                peers.AddRange (PeerInfo.FromCompact (str.Span, AddressFamily.InterNetwork));
                            break;

                        case ("peers6"):
                            if (keypair.Value is BEncodedList)          // Non-compact response
                                peers.AddRange (PeerDecoder.Decode ((BEncodedList) keypair.Value, AddressFamily.InterNetworkV6));
                            else if (keypair.Value is BEncodedString str)   // Compact response
                                peers.AddRange (PeerInfo.FromCompact (str.Span, AddressFamily.InterNetworkV6));
                            break;

                        default:
                            break;
                    }
                }
                m = new AnnounceResponseMessage (remotePeer.AddressFamily, announceMessage.TransactionId, interval, leechers, seeders, peers);
            }
            ReadOnlyMemory<byte> data = m.Encode ();
            await client.SendAsync (data, data.Length, remotePeer);
        }

        NameValueCollection getCollection (AnnounceMessage announceMessage)
        {
            var res = new NameValueCollection {
                { "info_hash", announceMessage.InfoHash!.UrlEncode () },
                { "peer_id", UriQueryBuilder.UrlEncodeQuery(announceMessage.PeerId.Span) },
                { "port", announceMessage.Port.ToString () },
                { "uploaded", announceMessage.Uploaded.ToString () },
                { "downloaded", announceMessage.Downloaded.ToString () },
                { "left", announceMessage.Left.ToString () },
                { "compact", "1" },//hardcode
                { "numwant", announceMessage.NumWanted.ToString () },
                { "ip", announceMessage.IP.ToString () },
                { "key", announceMessage.Key.ToString () },
                { "event", announceMessage.TorrentEvent.ToString ().ToLower () }
            };
            return res;
        }

        protected virtual async Task ReceiveScrape (UdpClient client, ScrapeMessage scrapeMessage, IPEndPoint remotePeer)
        {
            BEncodedDictionary val = Handle (getCollection (scrapeMessage), remotePeer.Address, true);

            UdpTrackerMessage m;
            ReadOnlyMemory<byte> data;
            if (val.ContainsKey (TrackerRequest.FailureKey)) {
                m = new ErrorMessage (scrapeMessage.TransactionId, val[TrackerRequest.FailureKey].ToString ()!);
            } else {
                var scrapes = new List<ScrapeDetails> ();

                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in val) {
                    var dict = (BEncodedDictionary) keypair.Value;
                    int seeds = 0;
                    int leeches = 0;
                    int complete = 0;
                    foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair2 in dict) {
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
            foreach (var v in scrapeMessage.InfoHashes)
                res.Add ("info_hash", v.UrlEncode ());
            return res;
        }

        protected virtual Task ReceiveError (UdpClient client, ErrorMessage errorMessage, IPEndPoint remotePeer)
        {
            throw new InvalidOperationException ($"ErrorMessage from :{remotePeer.Address}");
        }
    }
}
