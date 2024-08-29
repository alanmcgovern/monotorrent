using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
            var engine = EngineHelpers.Create (EngineHelpers.CreateSettings ());
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
            var builder = new EngineSettingsBuilder (EngineHelpers.CreateSettings ());
            builder.ConnectionTimeout = TimeSpan.FromHours (1);
            var engine = EngineHelpers.Create (
                builder.ToSettings (),
                EngineHelpers.Factories.WithPeerConnectionCreator ("ipv4",t => {
                    return fake;
                })
            );

            var connectionManager = new ConnectionManager ("test", engine.Settings, engine.Factories, engine.DiskManager);

            var manager = await engine.AddAsync (new MagnetLink (new InfoHash (Enumerable.Repeat ((byte) 0, 20).ToArray ())), "tmp");
            manager.Mode = new MetadataMode (manager, engine.DiskManager, connectionManager, engine.Settings, "");
            manager.Peers.AvailablePeers.Add (new Peer (new PeerInfo (fake.Uri), new[] { EncryptionType.PlainText }));
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
    }
}
