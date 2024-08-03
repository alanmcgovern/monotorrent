//
// MultiProtocolHttpTrackerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2023 Alan McGovern
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections.Tracker;
using MonoTorrent.Connections.TrackerServer;
using MonoTorrent.Messages.UdpTracker;
using MonoTorrent.TrackerServer;

using NUnit.Framework;

namespace MonoTorrent.Trackers
{
    [TestFixture]
    public class MultiProtocolHttpTrackerTests
    {
        static readonly TimeSpan Timeout = Debugger.IsAttached ? TimeSpan.FromDays (10) : TimeSpan.FromSeconds (10);

        static readonly ReadOnlyMemory<byte> PeerId = Enumerable.Repeat ((byte) 255, 20).ToArray ();
        static readonly InfoHash InfoHash = new InfoHash (Enumerable.Repeat ((byte) 254, 20).ToArray ());

        TrackerServer.TrackerServer server;

        ITrackerConnection ConnectionIPv4, ConnectionIPv6;
        List<ITrackerListener> listeners;
        List<BEncodedString> ipv4keys, ipv6keys;
        List<InfoHash> ipv4announcedInfoHashes, ipv6announcedInfoHashes;
        List<InfoHash> ipv4scrapedInfoHashes, ipv6scrapedInfoHashes;

        AnnounceRequest announceparams = new AnnounceRequest (100, 50, 12345, TorrentEvent.Completed, InfoHashes.FromV1(InfoHash), false, PeerId, t => (null, 1515), false);

        [SetUp]
        public void Setup ()
        {
            ipv4keys = new List<BEncodedString> ();
            ipv6keys = new List<BEncodedString> ();
            ipv4announcedInfoHashes = new List<InfoHash> ();
            ipv6announcedInfoHashes = new List<InfoHash> ();
            ipv4scrapedInfoHashes = new List<InfoHash> ();
            ipv6scrapedInfoHashes = new List<InfoHash> ();

            var uri = new Uri ($"http://localhost:123/announce");
            ConnectionIPv4 = new HttpTrackerConnection (uri, Factories.Default.CreateHttpClient, AddressFamily.InterNetwork);
            ConnectionIPv6 = new HttpTrackerConnection (uri, Factories.Default.CreateHttpClient, AddressFamily.InterNetworkV6);

            server = new MonoTorrent.TrackerServer.TrackerServer ();
            server.AllowUnregisteredTorrents = true;

            listeners = new List<ITrackerListener> ();
        }

        [TearDown]
        public void Teardown ()
        {
            foreach (var listener in listeners) {
                server.UnregisterListener (listener);
                listener.Stop ();
            }
            server.Dispose ();
        }

        [Test]
        public async Task AnnounceBoth_BothActive ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv4, ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 and 5 ipv6, + self ipv4 + self ipv6
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (12, peersFromAnnounce.Length);
            foreach (var peer in addedPeers)
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceBoth_OnlyIPv4Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);

            var tracker = new Tracker (ConnectionIPv4, ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 + self ipv4
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetwork))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceBoth_OnlyIPv6Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv4, ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv6 + self ipv6
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetworkV6))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceIPv4_BothActive ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv4);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 + self ipv4
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetwork))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceIPv4_OnlyIPv4Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);

            var tracker = new Tracker (ConnectionIPv4);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 + self ipv4
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetwork))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceIPv4_OnlyIPv6Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv4);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Offline, result.State);

            // no peers and this fails.
            Assert.AreEqual (0, result.Peers.Count);
        }

        [Test]
        public async Task AnnounceIPv6_BothActive ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 + self ipv4
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetworkV6))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        [Test]
        public async Task AnnounceIPv6_OnlyIPv4Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetwork);

            var tracker = new Tracker (ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Offline, result.State);

            // no peers and this fails.
            Assert.AreEqual (0, result.Peers.Count);
        }

        [Test]
        public async Task AnnounceIPv6_OnlyIPv6Active ()
        {
            var addedPeers = AddPeersToServer (5, AddressFamily.InterNetwork, AddressFamily.InterNetworkV6);
            AddListener (AddressFamily.InterNetworkV6);

            var tracker = new Tracker (ConnectionIPv6);
            var result = await tracker.AnnounceAsync (announceparams, new CancellationTokenSource (Timeout).Token);
            Assert.AreEqual (TrackerState.Ok, result.State);

            // 5 ipv4 + self ipv4
            var peersFromAnnounce = result.Peers[InfoHash]
                .Select (t => new IPEndPoint (IPAddress.Parse (t.ConnectionUri.Host), t.ConnectionUri.Port))
                .ToArray ();
            Assert.AreEqual (6, peersFromAnnounce.Length);
            foreach (var peer in addedPeers.Where (t => t.AddressFamily == AddressFamily.InterNetworkV6))
                CollectionAssert.Contains (peersFromAnnounce, peer);
        }

        ITrackerListener AddListener (AddressFamily addressFamily)
        {
            HttpTrackerListener listener = null;

            // Try to work around port-in-use issues in CI. Urgh. This is awful :P 
            int preferredPort = -1;
            var portGenerator = new Random ();
            for (int i = 0; i < 100; i++) {
                preferredPort = portGenerator.Next (10000, 50000);

                if (addressFamily == AddressFamily.InterNetwork) {
                    listener = new HttpTrackerListener (new IPEndPoint (IPAddress.Loopback, preferredPort));
                    listener.AnnounceReceived += delegate (object o, MonoTorrent.TrackerServer.AnnounceRequest e) {
                        ipv4keys.Add (e.Key);
                        ipv4announcedInfoHashes.Add (e.InfoHash);
                    };
                    listener.ScrapeReceived += (o, e) => {
                        ipv4scrapedInfoHashes.AddRange (e.InfoHashes);
                    };
                } else if (addressFamily == AddressFamily.InterNetworkV6) {
                    listener = new HttpTrackerListener (new IPEndPoint (IPAddress.IPv6Loopback, preferredPort));
                    listener.AnnounceReceived += delegate (object o, MonoTorrent.TrackerServer.AnnounceRequest e) {
                        ipv6keys.Add (e.Key);
                        ipv6announcedInfoHashes.Add (e.InfoHash);
                    };
                    listener.ScrapeReceived += (o, e) => {
                        ipv6scrapedInfoHashes.AddRange (e.InfoHashes);
                    };
                } else {
                    throw new NotSupportedException ();
                }
                try {
                    listener.Start ();
                } catch {
                    continue;
                }
                server.RegisterListener (listener);
                listeners.Add (listener);
                break;
            }

            var uri = new Uri ($"http://localhost:{preferredPort}/announce");
            if (addressFamily == AddressFamily.InterNetwork)
                ConnectionIPv4 = new HttpTrackerConnection (uri, Factories.Default.CreateHttpClient, addressFamily);
            if (addressFamily == AddressFamily.InterNetworkV6)
                ConnectionIPv6 = new HttpTrackerConnection (uri, Factories.Default.CreateHttpClient, addressFamily);

            return listener;
        }

        List<IPEndPoint> AddPeersToServer (int count, params AddressFamily[] families)
        {
            List<IPEndPoint> addedPeers = new List<IPEndPoint> ();
            var manager = new SimpleTorrentManager (new InfoHashTrackable ("test", announceparams.InfoHashes.V1!), new ClientAddressComparer (), server);
            foreach (var family in families) {
                for (int i = 0; i < 5; i++) {
                    var peer = family switch {
                        AddressFamily.InterNetwork => new Peer (new IPEndPoint (IPAddress.Parse ($"192.168.9.{i}"), 1000 + i), $"ipv4-{i}"),
                        AddressFamily.InterNetworkV6 => new Peer (new IPEndPoint (IPAddress.Parse ($"::{i}.{i}.{i}.{i}"), 1000 + i), $"ipv6-{i}"),
                        _ => throw new NotSupportedException ()
                    };
                    manager.Add (peer);
                    addedPeers.Add (peer.ClientAddress);
                }
            }
            server.Add (manager);
            return addedPeers;
        }
    }
}
