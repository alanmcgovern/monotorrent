//
// Node.cs
//
// Authors:
//   Jérémie Laval <jeremie.laval@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Jérémie Laval, Alan McGovern
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
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Dht
{
    class Node : IEquatable<Node>
    {
        public static readonly int MaxFailures = 4;

        public IPEndPoint EndPoint { get; }
        public int FailedCount { get; set; }
        public NodeId Id { get; }
        public TimeSpan LastSeen => LastSeenTimer.Elapsed + LastSeenDelta;
        TimeSpan LastSeenDelta { get; set; }
        ValueStopwatch LastSeenTimer;
        public NodeState State {
            get {
                if (FailedCount >= MaxFailures)
                    return NodeState.Bad;

                else if (!LastSeenTimer.IsRunning)
                    return NodeState.Unknown;

                return LastSeen.TotalMinutes < 15 ? NodeState.Good : NodeState.Questionable;
            }
        }
        public BEncodedValue Token { get; set; }

        public Node (NodeId id, IPEndPoint endpoint)
        {
            EndPoint = endpoint;
            Id = id;

            LastSeenDelta = TimeSpan.FromDays (1);
            LastSeenTimer = new ValueStopwatch ();
        }

        internal void Seen ()
        {
            Seen (TimeSpan.Zero);
        }

        internal void Seen (TimeSpan delta)
        {
            FailedCount = 0;
            LastSeenDelta = delta;
            LastSeenTimer.Restart ();
        }

        internal BEncodedString CompactPort ()
        {
            byte[] buffer = new byte[6];
            CompactPort (buffer, 0);
            return buffer;
        }

        internal void CompactPort (byte[] buffer, int offset)
        {
            Message.Write (buffer, offset, EndPoint.Address.GetAddressBytes ());
            Message.Write (buffer, offset + 4, (ushort) EndPoint.Port);
        }

        internal static BEncodedString CompactPort (IList<Node> peers)
        {
            byte[] buffer = new byte[peers.Count * 6];
            for (int i = 0; i < peers.Count; i++)
                peers[i].CompactPort (buffer, i * 6);

            return new BEncodedString (buffer);
        }

        internal BEncodedString CompactNode ()
        {
            byte[] buffer = new byte[26];
            CompactNode (buffer, 0);
            return buffer;
        }

        void CompactNode (byte[] buffer, int offset)
        {
            Message.Write (buffer, offset, Id.Bytes);
            CompactPort (buffer, offset + 20);
        }

        internal static BEncodedString CompactNode (ICollection<Node> nodes)
        {
            int count = 0;
            byte[] buffer = new byte[nodes.Count * 26];
            foreach (Node node in nodes) {
                node.CompactNode (buffer, count * 26);
                count++;
            }

            return new BEncodedString (buffer);
        }

        internal static Node FromCompactNode (byte[] buffer, int offset)
        {
            byte[] id = new byte[20];
            Buffer.BlockCopy (buffer, offset, id, 0, 20);
            var address = new IPAddress ((uint) BitConverter.ToInt32 (buffer, offset + 20));
            int port = (ushort) IPAddress.NetworkToHostOrder ((short) BitConverter.ToUInt16 (buffer, offset + 24));
            return new Node (new NodeId (id), new IPEndPoint (address, port));
        }

        internal static IEnumerable<Node> FromCompactNode (byte[] buffer)
        {
            for (int i = 0; (i + 26) <= buffer.Length; i += 26)
                yield return FromCompactNode (buffer, i);
        }

        internal static IEnumerable<Node> FromCompactNode (BEncodedString nodes)
        {
            return FromCompactNode (nodes.TextBytes);
        }

        internal static IEnumerable<Node> FromCompactNode (BEncodedList nodes)
        {
            foreach (BEncodedValue node in nodes) {
                //bad format!
                if (!(node is BEncodedList))
                    continue;

                string host = string.Empty;
                long port = 0;
                foreach (BEncodedValue val in (BEncodedList) node) {
                    if (val is BEncodedString)
                        host = ((BEncodedString) val).Text;
                    else if (val is BEncodedNumber)
                        port = ((BEncodedNumber) val).Number;
                }

                IPAddress.TryParse (host, out IPAddress address);

                //REM: bad design from bitcomet we do not have node id so create it...
                //or use torrent infohash?
                // Will messages from this node be discarded later on if the NodeId doesn't match?
                if (address != null)
                    yield return new Node (NodeId.Create (), new IPEndPoint (address, (int) port));
            }
        }

        public override bool Equals (object obj)
        {
            return Equals (obj as Node);
        }

        public bool Equals (Node other)
        {
            return Id.Equals (other?.Id);
        }

        public override int GetHashCode ()
        {
            return Id.GetHashCode ();
        }

        public override string ToString ()
        {
            var sb = new StringBuilder (48);
            for (int i = 0; i < Id.Bytes.Length; i++) {
                sb.Append (Id.Bytes[i]);
                sb.Append ("-");
            }
            return sb.ToString (0, sb.Length - 1);
        }
    }
}
