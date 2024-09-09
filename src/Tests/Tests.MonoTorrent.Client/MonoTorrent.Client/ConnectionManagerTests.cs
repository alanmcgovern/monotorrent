using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.Modes;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Messages.Peer;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class ConnectionManagerTests
    {
        [Test]
        public async Task SortByLeastConnections ()
        {
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings (allowedEncryption: new[] { EncryptionType.PlainText }));
            var manager = new ConnectionManager ("test", engine.Settings, engine.Factories, engine.DiskManager);

            var torrents = new[] {
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ())), "tmp"),
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 1, 20).ToArray ())), "tmp"),
                await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 2, 20).ToArray ())), "tmp")
            };

            torrents[0].Peers.ConnectedPeers.Add (PeerId.CreateNull (1, torrents[0].InfoHashes.V1OrV2));
            torrents[0].Peers.ConnectedPeers.Add (PeerId.CreateNull (1, torrents[0].InfoHashes.V1OrV2));
            torrents[2].Peers.ConnectedPeers.Add (PeerId.CreateNull (1, torrents[2].InfoHashes.V1OrV2));

            foreach (var torrent in torrents)
                manager.Add (torrent);

            manager.TryConnect ();

            Assert.AreEqual (torrents[1], manager.Torrents[0]);
            Assert.AreEqual (torrents[2], manager.Torrents[1]);
            Assert.AreEqual (torrents[0], manager.Torrents[2]);
        }

        class FakeConnection : IPeerConnection
        {
            public ReadOnlyMemory<byte> AddressBytes { get; }
            public bool CanReconnect { get; }
            public bool Disposed { get; private set; }
            public IPEndPoint EndPoint { get; }
            public bool IsIncoming { get; }
            public Uri Uri { get; }

            public FakeConnection (Uri uri)
                => Uri = uri;

            public ReusableTaskCompletionSource<bool> ConnectAsyncInvokedTask = new ReusableTaskCompletionSource<bool> ();
            public ReusableTaskCompletionSource<bool> ConnectAsyncResultTask = new ReusableTaskCompletionSource<bool> ();
            public async ReusableTask ConnectAsync ()
            {
                ConnectAsyncInvokedTask.SetResult (true);
                await ConnectAsyncResultTask.Task;
            }

            public TaskCompletionSource<bool> DisposeAsyncInvokedTask = new TaskCompletionSource<bool> ();
            public void Dispose ()
            {
                TestContext.Out.WriteLine (Environment.StackTrace);
                Disposed = true;
                ConnectAsyncResultTask.SetException (new SocketException ((int) SocketError.ConnectionAborted));
                DisposeAsyncInvokedTask.SetResult (true);
            }

            public ReusableTaskCompletionSource<Memory<byte>> ReceiveAsyncInvokedTask = new ReusableTaskCompletionSource<Memory<byte>> ();
            public ReusableTaskCompletionSource<int> ReceiveAsyncResultTask = new ReusableTaskCompletionSource<int> ();
            public async ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
            {
                ReceiveAsyncInvokedTask.SetResult (buffer);
                return await ReceiveAsyncResultTask.Task;
            }

            public ReusableTaskCompletionSource<Memory<byte>> SendAsyncInvokedTask = new ReusableTaskCompletionSource<Memory<byte>> ();
            public ReusableTaskCompletionSource<int> SendAsyncResultTask = new ReusableTaskCompletionSource<int> ();
            public async ReusableTask<int> SendAsync (Memory<byte> buffer)
            {
                SendAsyncInvokedTask.SetResult (buffer);
                return await SendAsyncResultTask.Task;
            }
        }

        [Test]
        public async Task CancelPending_WaitingForConnect ()
        {
            var fake = new FakeConnection (new Uri ("ipv4://1.2.3.4:56789"));
            var engine = EngineHelpers.Create (
                EngineHelpers.CreateSettings (),
                EngineHelpers.Factories.WithPeerConnectionCreator ("ipv4", t => fake)
            );

            var connectionManager = new ConnectionManager ("test", engine.Settings, engine.Factories, engine.DiskManager);

            var manager = await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ())), "tmp");
            manager.Mode = new MetadataMode (manager, engine.DiskManager, connectionManager, engine.Settings, "");
            manager.Peers.AvailablePeers.Add (PeerId.CreateNull (1, manager.InfoHashes.V1OrV2).Peer);
            connectionManager.Add (manager);

            await ClientEngine.MainLoop;

            // Initiate a connection
            connectionManager.TryConnect ();
            await fake.ConnectAsyncInvokedTask.Task.WithTimeout ();

            // Abort it while we're waiting for the connection to succeed.
            connectionManager.CancelPendingConnects (manager);

            // Make sure the connection was disposed.
            await fake.DisposeAsyncInvokedTask.Task.WithTimeout ();
            Assert.IsTrue (fake.Disposed);
        }

        [Test]
        public async Task CancelPending_SendingHandshake ()
        {
            var fake = new FakeConnection (new Uri ("ipv4://1.2.3.4:56789"));
            var builder = new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                ConnectionTimeout = TimeSpan.FromHours (1),
                AllowedEncryption = new System.Collections.Generic.List<EncryptionType> { EncryptionType.PlainText }
            };
            var engine = EngineHelpers.Create (
                builder.ToSettings (),
                EngineHelpers.Factories.WithPeerConnectionCreator ("ipv4", t => {
                    return fake;
                })
            );

            var connectionManager = new ConnectionManager ("test", engine.Settings, engine.Factories, engine.DiskManager);

            var manager = await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ())), "tmp");
            manager.Mode = new MetadataMode (manager, engine.DiskManager, connectionManager, engine.Settings, "");
            manager.Peers.AvailablePeers.Add (new Peer (new PeerInfo (fake.Uri)));
            connectionManager.Add (manager);

            await ClientEngine.MainLoop;

            // Initiate a connection and allow it to succeed
            connectionManager.TryConnect ();
            await fake.ConnectAsyncInvokedTask.Task.WithTimeout ();
            fake.ConnectAsyncResultTask.SetResult (true);

            // Handshake should be sent.
            var data = await fake.SendAsyncInvokedTask.Task.WithTimeout ();
            var message = new HandshakeMessage (data.Span);
            Assert.AreEqual (message.ProtocolString, Constants.ProtocolStringV100);

            connectionManager.CancelPendingConnects (manager);
            await fake.DisposeAsyncInvokedTask.Task.WithTimeout ();
            Assert.IsTrue (fake.Disposed);
        }

        [Test]
        public async Task ConnectToSelf ()
        {
            var seeder = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new List<EncryptionType> { EncryptionType.RC4Header, EncryptionType.PlainText, EncryptionType.RC4Full },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());

            var magnetLink = new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ()));
            var seederManager = await seeder.AddAsync (magnetLink, "tmp_seeder");

            var ready = seederManager.WaitForState (TorrentState.Metadata);
            await seederManager.StartAsync ();
            await ready.WithTimeout ();

            var failedPeer = new TaskCompletionSource<ConnectionAttemptFailedEventArgs> ();
            seederManager.ConnectionAttemptFailed += (o, e) => failedPeer.SetResult (e);

            // Connect to self
            await seederManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{seeder.PeerListeners[0].LocalEndPoint.Port}")));

            var failedConnection = await failedPeer.Task;
            Assert.AreEqual (ConnectionFailureReason.ConnectedToSelf, failedConnection.Reason);
        }

        [Test]
        public async Task EncryptionTiers_LastMatches ([Values (true, false)] bool addToSeeder)
        {
            int failedCount = 0;
            var seeder = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new List<EncryptionType> { EncryptionType.RC4Header, EncryptionType.PlainText, EncryptionType.RC4Full },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());
            var leecher = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new System.Collections.Generic.List<EncryptionType> { EncryptionType.RC4Full },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());

            var magnetLink = new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ()));
            var seederManager = await seeder.AddAsync (magnetLink, "tmp_seeder");
            var leecherManager = await leecher.AddAsync (magnetLink, "tmp_seeder");

            var ready = Task.WhenAll (seederManager.WaitForState (TorrentState.Metadata), leecherManager.WaitForState (TorrentState.Metadata));
            await seederManager.StartAsync ();
            await leecherManager.StartAsync ();
            await ready;

            var seederConnected = new TaskCompletionSource<PeerId> ();
            seederManager.PeerConnected += (o, e) => seederConnected.TrySetResult (e.Peer);

            var leecherConnected = new TaskCompletionSource<PeerId> ();
            leecherManager.PeerConnected += (o, e) => leecherConnected.TrySetResult (e.Peer);

            seederManager.ConnectionAttemptFailed += (o, e) => failedCount++;
            leecherManager.ConnectionAttemptFailed += (o, e) => failedCount++;

            if (addToSeeder)
                await seederManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{leecher.PeerListeners[0].LocalEndPoint.Port}")));
            else
                await leecherManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{seeder.PeerListeners[0].LocalEndPoint.Port}")));
            await seederConnected.Task.WithTimeout ();
            await leecherConnected.Task.WithTimeout ();

            var connectedSeeder = (await seederManager.GetPeersAsync ()).Single ();
            Assert.AreEqual (0, connectedSeeder.Peer.CleanedUpCount);
            Assert.AreEqual (0, connectedSeeder.Peer.FailedConnectionAttempts);
            Assert.AreEqual (EncryptionType.RC4Full, connectedSeeder.EncryptionType);

            connectedSeeder = (await leecherManager.GetPeersAsync ()).Single ();
            Assert.AreEqual (0, connectedSeeder.Peer.CleanedUpCount);
            Assert.AreEqual (0, connectedSeeder.Peer.FailedConnectionAttempts);
            Assert.AreEqual (EncryptionType.RC4Full, connectedSeeder.EncryptionType);

            Assert.AreEqual (0, failedCount);

            await seeder.StopAsync ();
            await leecher.StopAsync ();
        }


        [Test]
        public async Task EncryptionTiers_NoneMatch ([Values (true, false)] bool addToSeeder)
        {
            int failedCount = 0;
            var seeder = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new List<EncryptionType> { EncryptionType.RC4Header, EncryptionType.PlainText },
                ConnectionRetryDelays = new List<TimeSpan> { TimeSpan.FromDays (1) },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());
            var leecher = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new System.Collections.Generic.List<EncryptionType> { EncryptionType.RC4Full },
                ConnectionRetryDelays = new List<TimeSpan> { TimeSpan.FromDays (1) },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());

            var magnetLink = new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ()));
            var seederManager = await seeder.AddAsync (magnetLink, "tmp_seeder");
            var leecherManager = await leecher.AddAsync (magnetLink, "tmp_seeder");

            var ready = Task.WhenAll (seederManager.WaitForState (TorrentState.Metadata), leecherManager.WaitForState (TorrentState.Metadata));
            await seederManager.StartAsync ();
            await leecherManager.StartAsync ();
            await ready;

            var peerFailedTask = new TaskCompletionSource<bool> ();

            EventHandler<ConnectionAttemptFailedEventArgs> handler = (o, e) => {
                failedCount++;
                peerFailedTask.SetResult (true);
            };
            seederManager.ConnectionAttemptFailed += handler;
            leecherManager.ConnectionAttemptFailed += handler;

            if (addToSeeder)
                await seederManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{leecher.PeerListeners[0].LocalEndPoint.Port}")));
            else
                await leecherManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{seeder.PeerListeners[0].LocalEndPoint.Port}")));
            await peerFailedTask.Task.WithTimeout ();

            Assert.AreEqual (0, (await seederManager.GetPeersAsync ()).Count);
            Assert.AreEqual (0, (await leecherManager.GetPeersAsync ()).Count);

            // It failed to connect once (even though we attempted multiple tiers)
            Assert.AreEqual (1, failedCount);
            var peer = addToSeeder ? seederManager.Peers.AvailablePeers[0] : leecherManager.Peers.AvailablePeers[0];
            Assert.AreEqual (1, peer.FailedConnectionAttempts);
        }

        [Test]
        public async Task RetryConnection_AfterTimeout ()
        {
            // The first retry should happen immediately, a second retry should take 'forever'.
            var seeder = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                ConnectionRetryDelays = new List<TimeSpan> { TimeSpan.FromSeconds (1000), TimeSpan.FromSeconds (3000) },
                AllowedEncryption = new List<EncryptionType> { EncryptionType.RC4Header, EncryptionType.PlainText, EncryptionType.RC4Full },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());

            var leecher = EngineHelpers.Create (new EngineSettingsBuilder (EngineHelpers.CreateSettings ()) {
                AllowLocalPeerDiscovery = false,
                AllowedEncryption = new System.Collections.Generic.List<EncryptionType> { EncryptionType.RC4Full },
                ListenEndPoints = new Dictionary<string, IPEndPoint> { { "ipv4", new IPEndPoint (IPAddress.Loopback, 0) } },
            }.ToSettings ());

            var magnetLink = new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ()));
            var seederManager = await seeder.AddAsync (magnetLink, "tmp_seeder");
            var leecherManager = await leecher.AddAsync (magnetLink, "tmp_seeder");

            var ready = Task.WhenAll (seederManager.WaitForState (TorrentState.Metadata), leecherManager.WaitForState (TorrentState.Metadata));
            await seederManager.StartAsync ();
            await leecherManager.StartAsync ();
            await ready;

            var seederDisconnected = new ReusableTaskCompletionSource<PeerId> ();
            seederManager.PeerDisconnected += (o, e) => seederDisconnected.SetResult (e.Peer);

            var leecherDisconnected = new ReusableTaskCompletionSource<PeerId> ();
            leecherManager.PeerDisconnected += (o, e) => leecherDisconnected.SetResult (e.Peer);

            var seederConnected = new ReusableTaskCompletionSource<PeerId> ();
            seederManager.PeerConnected += (o, e) => seederConnected.SetResult (e.Peer);

            var leecherConnected = new ReusableTaskCompletionSource<PeerId> ();
            leecherManager.PeerConnected += (o, e) => leecherConnected.SetResult (e.Peer);

            int failedCount = 0;
            seederManager.ConnectionAttemptFailed += (o, e) => failedCount++;
            leecherManager.ConnectionAttemptFailed += (o, e) => failedCount++;

            await seederManager.AddPeerAsync (new PeerInfo (new Uri ($"ipv4://127.0.0.1:{leecher.PeerListeners[0].LocalEndPoint.Port}")));

            var seederPeer = await seederConnected.Task.WithTimeout ();
            var leecherPeer = await leecherConnected.Task.WithTimeout ();
            Assert.AreEqual (0, seederPeer.Peer.CleanedUpCount);

            // Close the connections
            seederPeer.Connection.Dispose ();
            leecherPeer.Connection.Dispose ();

            // Send a keepalive message so the engine definitively sees that the socket has been closed.
            seederPeer.LastMessageSent = ValueStopwatch.WithTime (TimeSpan.FromSeconds (100));
            leecherPeer.LastMessageSent = ValueStopwatch.WithTime (TimeSpan.FromSeconds (100));

            Assert.IsTrue (seederPeer == await seederDisconnected.Task.WithTimeout ());
            Assert.IsTrue (leecherPeer == await leecherDisconnected.Task.WithTimeout ());

            seederManager.Peers.AvailablePeers[0].WaitUntilNextConnectionAttempt = ValueStopwatch.WithTime (TimeSpan.FromSeconds(2000));

            // Wait for the retry!
            seederPeer = await seederConnected.Task.WithTimeout ();
            leecherPeer = await leecherConnected.Task.WithTimeout ();

            // Ensure we haven't double-disposed the peer
            Assert.AreEqual (1, seederPeer.Peer.CleanedUpCount);

            await seeder.StopAsync ();
            await leecher.StopAsync ();
        }

        [Test]
        public async Task DisposeTwice ()
        {
            var magnetLink = new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ()));
            var seeder = EngineHelpers.Create ();
            var seederManager = await seeder.AddAsync (magnetLink, "tmp_seeder");

            var peerId = PeerId.CreateNull (seederManager.Bitfield.Length, seederManager.InfoHashes.V1OrV2.Truncate ());

            // Each active connection will only be disposed once, even if it is cleaned up multiple times.
            // It's normal for there to be at least two calls two cleanup - one from any ongoing 'SendAsync' call
            // and one from ongoing 'ReceiveAsync' call.
            for (int i = 0; i < 3; i++)
                seeder.ConnectionManager.CleanupSocket (seederManager, peerId);
            Assert.AreEqual (1, peerId.Peer.CleanedUpCount);

            peerId = new PeerId (peerId.Peer, NullConnection.Outgoing, peerId.MutableBitField, peerId.ExpectedInfoHash, peerId.Encryptor, peerId.Decryptor, peerId.ClientApp);
            for (int i = 0; i < 3; i++)
                seeder.ConnectionManager.CleanupSocket (seederManager, peerId);
            Assert.AreEqual (2, peerId.Peer.CleanedUpCount);
        }
    }
}
