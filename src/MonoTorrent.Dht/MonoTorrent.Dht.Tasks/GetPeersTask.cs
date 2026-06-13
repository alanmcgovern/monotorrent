//
// GetPeersTask.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class GetPeersTask
    {
        const int MaxPeers = 128;

        HashSet<PeerInfo> FoundPeers { get; }

        DhtEngine Engine { get; }
        NodeId InfoHash { get; }

        public GetPeersTask (DhtEngine engine, InfoHash infohash)
            : this (engine, new NodeId (infohash.Span))
        {

        }

        public GetPeersTask (DhtEngine engine, NodeId infohash)
        {
            Engine = engine;
            InfoHash = infohash;
            FoundPeers = new HashSet<PeerInfo> ();
        }

        public async Task<IEnumerable<Node>> ExecuteAsync ()
        {
            DhtEngine.MainLoop.CheckThread ();

            var activeQueries = new List<Task<SendQueryEventArgs>> ();
            var closestNodes = new ClosestNodesCollection (InfoHash);
            var closestActiveNodes = new ClosestNodesCollection (InfoHash);

            foreach (Node node in Engine.RoutingTable.GetClosest (InfoHash)) {
                if (closestNodes.Add (node)) {
                    var transactionId = TransactionId.NextId ();
                    var query = KrpcMessageEncoder.EncodeGetPeers (transactionId, Engine.LocalId, InfoHash.Span);
                    activeQueries.Add (Engine.SendQueryAsync (query, node).AsTask ());
                }
            }

            while (activeQueries.Count > 0) {
                var completed = await Task.WhenAny (activeQueries);
                activeQueries.Remove (completed);

                // If it timed out or failed just move to the next query.
                SendQueryEventArgs query = await completed;
                if (query.Response.IsEmpty)
                    continue;

                var response = KrpcMessage.Parse (query.Response);

                // Values is a bencoded list: l <peer> <peer> ... e
                if (!response.Response.Values.IsEmpty && FoundPeers.Count < MaxPeers) {

                    var peerArray = ParseValues (response.Response.Values);
                    if (peerArray.Count > 0) {
                        foreach (var peer in peerArray)
                            FoundPeers.Add (peer);
                        Engine.RaisePeersFound (InfoHash, peerArray);
                    }
                }
                // The response contains nodes which should be closer to our target. If they are closer than nodes
                // we've already checked, then let's query them!
                if (!response.Response.Nodes.IsEmpty && FoundPeers.Count < MaxPeers) {
                    foreach (Node node in Node.FromCompactNodes (response.Response.Nodes))
                        if (closestNodes.Add (node)) {
                            var transactionId = TransactionId.NextId ();
                            var getPeers = KrpcMessageEncoder.EncodeGetPeers (transactionId, Engine.LocalId, InfoHash.Span);
                            activeQueries.Add (Engine.SendQueryAsync (getPeers, node).AsTask ());
                        }
                }

                closestActiveNodes.Add (query.Node);
            }

            // Finally, return the 8 closest nodes we discovered during this phase. These are the nodes we should
            // announce to later.
            return closestActiveNodes;
        }

        List<PeerInfo> ParseValues (ReadOnlySpan<byte> peersSpan)
        {
            var reader = new BEncodeReader (peersSpan);
            var peers = new List<PeerInfo> ();

            // first token must be ListBegin
            if (!reader.Read () || reader.Token != BEncodeToken.ListBegin)
                return peers;

            while (true) {
                if (!reader.Read ())
                    break;

                if (reader.Token == BEncodeToken.ListEnd)
                    break;

                if (reader.Token != BEncodeToken.String)
                    throw new BEncodingException ("Expected compact peer string in values list.");

                var parsed = PeerInfo.FromCompact (reader.Span, Engine.AddressFamily);
                peers.AddRange (parsed);
            }

            return peers;
        }
    }
}
