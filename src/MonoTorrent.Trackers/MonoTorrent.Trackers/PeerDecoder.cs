//
// PeerDecoder.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Net.Sockets;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    static class PeerDecoder
    {
        public static IList<PeerInfo> Decode (BEncodedList peers, AddressFamily addressFamily)
        {
            var list = new List<PeerInfo> (peers.Count);
            foreach (BEncodedValue value in peers) {
                try {
                    if (value is BEncodedDictionary dict)
                        list.Add (DecodeFromDict (dict));
                    else if (value is BEncodedString str)
                        foreach (var p in PeerInfo.FromCompact (str.Span, addressFamily))
                            list.Add (p);
                } catch {
                    // If something is invalid and throws an exception, ignore it
                    // and continue decoding the rest of the peers
                }
            }
            return list;
        }

        static readonly BEncodedString PeerIdKey = new BEncodedString ("peer id");
        static readonly BEncodedString Peer_IdKey = new BEncodedString ("peer_id");
        static readonly BEncodedString IPKey = new BEncodedString ("ip");
        static readonly BEncodedString PortKey = new BEncodedString ("port");

        static PeerInfo DecodeFromDict (BEncodedDictionary dict)
        {
            BEncodedString peerId;

            if (dict.ContainsKey (PeerIdKey))
                peerId = (BEncodedString) dict[PeerIdKey];
            else if (dict.ContainsKey (Peer_IdKey))       // HACK: Some trackers return "peer_id" instead of "peer id"
                peerId = (BEncodedString) dict[Peer_IdKey];
            else
                peerId = BEncodedString.Empty;

            var ipAddress = IPAddress.Parse (((BEncodedString)dict[IPKey]).Text);
            int port = (int)((BEncodedNumber) dict[PortKey]).Number;
            var connectionUri = new Uri ($"{(ipAddress.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6")}://{new IPEndPoint (ipAddress, port)}");
            return new PeerInfo (connectionUri, peerId);
        }
    }
}
