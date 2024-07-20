//
// TrackerTestRig.cs
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
using System.Collections.Specialized;
using System.Net;
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.TrackerServer;

namespace MonoTorrent.TrackerServer
{
    public class CustomComparer : IPeerComparer
    {
        public new bool Equals (object left, object right)
        {
            return left.Equals (right);
        }
        public object GetKey (AnnounceRequest parameters)
        {
            return parameters.Uploaded;
        }

        public int GetHashCode (object obj)
        {
            return obj.GetHashCode ();
        }
    }

    class CustomListener : TrackerListener
    {
        public BEncodedValue Handle (PeerDetails d, TorrentEvent e, ITrackable trackable)
        {
            NameValueCollection c = new NameValueCollection ();
            c.Add ("info_hash", trackable.InfoHash.UrlEncode ());
            c.Add ("peer_id", System.Web.HttpUtility.UrlEncode (d.peerId.Span.ToArray ()).Replace("+", "%20"));
            c.Add ("port", d.Port.ToString ());
            c.Add ("uploaded", d.Uploaded.ToString ());
            c.Add ("downloaded", d.Downloaded.ToString ());
            c.Add ("left", d.Remaining.ToString ());
            c.Add ("compact", "0");

            return base.Handle (c, d.ClientAddress, false);
        }

        protected override void StartCore (CancellationToken token)
        {

        }
    }

    public class Trackable : ITrackable
    {
        public Trackable (InfoHash infoHash, string name)
        {
            this.InfoHash = infoHash;
            this.Name = name;
        }

        public InfoHash InfoHash { get; }

        public string Name { get; }
    }

    public class PeerDetails
    {
        public int Port;
        public IPAddress ClientAddress;
        public long Downloaded;
        public long Uploaded;
        public long Remaining;
        public BEncodedString peerId;
        public ITrackable trackable;
    }

    class TrackerTestRig : IDisposable
    {
        private readonly Random r = new Random (1000);

        public CustomListener Listener;
        public TrackerServer Tracker;

        public List<PeerDetails> Peers;
        public List<Trackable> Trackables;

        public TrackerTestRig ()
        {
            Tracker = new TrackerServer ();
            Listener = new CustomListener ();
            Tracker.RegisterListener (Listener);

            GenerateTrackables ();
            GeneratePeers ();
        }

        private void GenerateTrackables ()
        {
            Trackables = new List<Trackable> ();
            for (int i = 0; i < 10; i++) {
                byte[] infoHash = new byte[20];
                r.NextBytes (infoHash);
                Trackables.Add (new Trackable (new InfoHash (infoHash), i.ToString ()));
            }
        }

        private void GeneratePeers ()
        {
            Peers = new List<PeerDetails> ();
            for (int i = 0; i < 100; i++) {
                PeerDetails d = new PeerDetails ();
                d.ClientAddress = IPAddress.Parse ($"127.0.{i}.2");
                d.Downloaded = (int) (10000 * r.NextDouble ());
                d.peerId = $"-----------------{i:0.000}";
                d.Port = r.Next (65000);
                d.Remaining = r.Next (10000, 100000);
                d.Uploaded = r.Next (10000, 100000);
                Peers.Add (d);
            }
        }

        public void Dispose ()
        {
            Tracker.Dispose ();
            Listener.Stop ();
        }
    }
}
