﻿//
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

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    static class PeerDecoder
    {
        public static IList<PeerInfo> Decode (BEncodedList peers)
        {
            var list = new List<PeerInfo> (peers.Count);
            foreach (BEncodedValue value in peers) {
                try {
                    if (value is BEncodedDictionary)
                        list.Add (DecodeFromDict ((BEncodedDictionary) value));
                    else if (value is BEncodedString)
                        foreach (PeerInfo p in Decode ((BEncodedString) value))
                            list.Add (p);
                } catch {
                    // If something is invalid and throws an exception, ignore it
                    // and continue decoding the rest of the peers
                }
            }
            return list;
        }

        static PeerInfo DecodeFromDict (BEncodedDictionary dict)
        {
            BEncodedString peerId;

            if (dict.ContainsKey ("peer id"))
                peerId = (BEncodedString) dict["peer id"];
            else if (dict.ContainsKey ("peer_id"))       // HACK: Some trackers return "peer_id" instead of "peer id"
                peerId = (BEncodedString) dict["peer_id"];
            else
                peerId = BEncodedString.Empty;

            var connectionUri = new Uri ($"ipv4://{dict["ip"]}:{dict["port"]}");
            return new PeerInfo (connectionUri, peerId.AsMemory ());
        }

        public static IList<PeerInfo> Decode (BEncodedString peers)
            => PeerInfo.FromCompact (peers.Span);
    }
}
