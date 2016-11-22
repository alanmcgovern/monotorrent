//
// ConnectionManager.cs
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
using MonoTorrent.Common;
using System.Net.Sockets;
using System.Threading;
using MonoTorrent.Client.Encryption;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client
{
    internal delegate void MessagingCallback(PeerId id);

    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        #region Events

        public event EventHandler<AttemptConnectionEventArgs> BanPeer;

        /// <summary>
        /// Event that's fired every time a message is sent or Received from a Peer
        /// </summary>
        public event EventHandler<PeerMessageEventArgs> PeerMessageTransferred;

        #endregion


        #region Member Variables
        private ClientEngine engine;
        internal static readonly int ChunkLength = 2096 + 64;   // Download in 2kB chunks to allow for better rate limiting

        // Create the callbacks and reuse them. Reduces ongoing allocations by a fair few megs
        private AsyncMessageReceivedCallback peerHandshakeReceivedCallback;
        private MessagingCallback handshakeSentCallback;
        private MessagingCallback messageSentCallback;

        private AsyncCallback endCheckEncryptionCallback;
        private AsyncIOCallback endCreateConnectionCallback;
        internal AsyncIOCallback incomingConnectionAcceptedCallback;
        private AsyncIOCallback endSendMessageCallback;
        internal AsyncMessageReceivedCallback messageReceivedCallback;

        private List<AsyncConnectState> pendingConnects;

        /// <summary>
        /// The number of half open connections
        /// </summary>
        public int HalfOpenConnections
        {
            get { return NetworkIO.HalfOpens; }
        }


        /// <summary>
        /// The maximum number of half open connections
        /// </summary>
        public int MaxHalfOpenConnections
        {
            get { return this.engine.Settings.GlobalMaxHalfOpenConnections; }
        }


        /// <summary>
        /// The number of open connections
        /// </summary>
        public int OpenConnections
        {
            get
            {
                return (int) ClientEngine.MainLoop.QueueWait (() =>
                    (int) Toolbox.Accumulate<TorrentManager>(engine.Torrents, (m) =>
                        m.Peers.ConnectedPeers.Count
                    )
                );
            }
        }


        /// <summary>
        /// The maximum number of open connections
        /// </summary>
        public int MaxOpenConnections
        {
            get { return this.engine.Settings.GlobalMaxConnections; }
        }

        int TryConnectIndex {
            get; set;
        }
        #endregion


        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        public ConnectionManager(ClientEngine engine)
        {
            this.engine = engine;

            this.endCheckEncryptionCallback = ClientEngine.MainLoop.Wrap(EndCheckEncryption);
            this.endSendMessageCallback = (a, b, c) => ClientEngine.MainLoop.Queue(() => EndSendMessage(a, b, c));
            this.endCreateConnectionCallback = (a, b, c) => ClientEngine.MainLoop.Queue (() => EndCreateConnection (a, b, c));
            this.incomingConnectionAcceptedCallback = (a, b, c) => ClientEngine.MainLoop.Queue (() => IncomingConnectionAccepted(a, b, c));

            this.handshakeSentCallback = PeerHandshakeSent;
            this.peerHandshakeReceivedCallback = (a, b, c) => ClientEngine.MainLoop.Queue (() => PeerHandshakeReceived (a, b, c));
            this.messageSentCallback = PeerMessageSent;
            this.messageReceivedCallback = (a, b, c) => ClientEngine.MainLoop.Queue (() => MessageReceived (a, b, c));

            this.pendingConnects = new List<AsyncConnectState>();
        }

        #endregion


        #region Async Connection Methods

        internal void ConnectToPeer(TorrentManager manager, Peer peer)
        {
            // Connect to the peer.
            IConnection connection = ConnectionFactory.Create(peer.ConnectionUri);
            if (connection == null)
                return;

            peer.LastConnectionAttempt = DateTime.Now;
            AsyncConnectState c = new AsyncConnectState(manager, peer, connection);
            pendingConnects.Add(c);

            manager.Peers.ConnectingToPeers.Add(peer);
            NetworkIO.EnqueueConnect(connection, endCreateConnectionCallback, c);
        }

        private void EndCreateConnection(bool succeeded, int count, object state)
        {
            AsyncConnectState connect = (AsyncConnectState)state;
            pendingConnects.Remove(connect);
            if (connect.Manager.Engine == null || 
                !connect.Manager.Mode.CanAcceptConnections)
            {
                connect.Connection.Dispose();
                return;
            }
            
            try
            {
                connect.Manager.Peers.ConnectingToPeers.Remove(connect.Peer);
                if (!succeeded)
                {
                    Logger.Log(null, "ConnectionManager - Failed to connect{0}", connect.Peer);

                    connect.Manager.RaiseConnectionAttemptFailed(
                        new PeerConnectionFailedEventArgs(connect.Manager, connect.Peer, Direction.Outgoing, "EndCreateConnection"));
                    
                    connect.Peer.FailedConnectionAttempts++;
                    connect.Connection.Dispose();
                    connect.Manager.Peers.BusyPeers.Add(connect.Peer);
                }
                else
                {
                    PeerId id = new PeerId(connect.Peer, connect.Manager);
                    id.Connection = connect.Connection;
                    connect.Manager.Peers.ActivePeers.Add(connect.Peer);

                    Logger.Log(id.Connection, "ConnectionManager - Connection opened");

                    ProcessFreshConnection(id);
                }
            }

            catch (Exception)
            {
                // FIXME: Do nothing now?
            }
            finally
            {
                // Try to connect to another peer
                TryConnect();
            }
        }

        internal void ProcessFreshConnection(PeerId id)
        {
            // If we have too many open connections, close the connection
            if (OpenConnections > this.MaxOpenConnections)
            {
                CleanupSocket (id, "Too many connections");
                return;
            }

            try
            {
                id.ProcessingQueue = true;
                // Increase the count of the "open" connections
                EncryptorFactory.BeginCheckEncryption(id, 0, this.endCheckEncryptionCallback, id);
                
                id.TorrentManager.Peers.ConnectedPeers.Add(id);
                id.WhenConnected = DateTime.Now;
                // Baseline the time the last block was received
                id.LastBlockReceived = DateTime.Now;
            }
            catch (Exception)
            {
                id.TorrentManager.RaiseConnectionAttemptFailed(
                    new PeerConnectionFailedEventArgs(id.TorrentManager, id.Peer, Direction.Outgoing, "ProcessFreshConnection: failed to encrypt"));

                id.Connection.Dispose();
                id.Connection = null;
            }
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            PeerId id = (PeerId)result.AsyncState;
            byte[] initialData;
            try
            {
                EncryptorFactory.EndCheckEncryption(result, out initialData);
                if (initialData != null && initialData.Length > 0)
                    throw new EncryptionException("unhandled initial data");

                EncryptionTypes e = engine.Settings.AllowedEncryption;
                if (id.Encryptor is RC4 && !Toolbox.HasEncryption(e, EncryptionTypes.RC4Full) ||
                    id.Encryptor is RC4Header && !Toolbox.HasEncryption(e, EncryptionTypes.RC4Header) ||
                    id.Encryptor is PlainTextEncryption && !Toolbox.HasEncryption(e, EncryptionTypes.PlainText))
                {
                    CleanupSocket(id, id.Encryptor.GetType().Name + " encryption is not enabled");
                }
                else
                {
                    // Create a handshake message to send to the peer
                    HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
                    SendMessage(id, handshake, this.handshakeSentCallback);
                }
            }
            catch
            {
                id.Peer.Encryption &= ~EncryptionTypes.RC4Full;
                id.Peer.Encryption &= ~EncryptionTypes.RC4Header;
                CleanupSocket(id, "Failed encryptor check");
            }
        }

        private void EndSendMessage(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;
            if (!succeeded)
            {
                CleanupSocket (id, "Could not send message");
                return;
            }

            try
            {

                // Invoke the callback which we were told to invoke after we sent this message
                id.MessageSentCallback(id);
            }
            catch (Exception)
            {
                CleanupSocket (id, "Could not send message");
            }
        }

        private void PeerHandshakeSent(PeerId id)
        {
            PeerIO.EnqueueReceiveHandshake (id.Connection, id.Decryptor, peerHandshakeReceivedCallback, id);
        }

        private void PeerHandshakeReceived(bool succeeded, PeerMessage message, object state)
        {
            PeerId id = (PeerId)state;
            if (!succeeded)
            {
                CleanupSocket(id, "Handshaking failed");
                return;
            }

            try
            {
                message.Handle(id);

                // If there are any pending messages, send them otherwise set the queue
                // processing as finished.
                if (id.QueueLength > 0)
                    id.ConnectionManager.ProcessQueue(id);
                else
                    id.ProcessingQueue = false;

                PeerIO.EnqueueReceiveMessage (id.Connection, id.Decryptor, id.TorrentManager.DownloadLimiter, id.Monitor, id.TorrentManager, messageReceivedCallback, id);
                // Alert the engine that there is a new usable connection
                id.TorrentManager.HandlePeerConnected(id, Direction.Outgoing);
            }
            catch (TorrentException ex)
            {
                CleanupSocket(id, ex.Message);
            }
        }

        private void PeerMessageSent(PeerId id)
        {
            // If the peer has been cleaned up, just return.
            if (id.Connection == null)
                return;

            // Fire the event to let the user know a message was sent
            RaisePeerMessageTransferred(new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)id.CurrentlySendingMessage, Direction.Outgoing, id));

            id.LastMessageSent = DateTime.Now;
            this.ProcessQueue(id);
        }

        private void SendMessage(PeerId id, PeerMessage message, MessagingCallback callback)
        {
            try
            {
                id.MessageSentCallback = callback;
                id.CurrentlySendingMessage = message;

                RateLimiterGroup limiter = id.TorrentManager.UploadLimiter;
                
                if (message is PieceMessage)
                {
                    PeerIO.EnqueueSendMessage (id.Connection, id.Encryptor, message, limiter, id.Monitor, id.TorrentManager.Monitor, endSendMessageCallback, id);
                    ClientEngine.BufferManager.FreeBuffer(ref ((PieceMessage)message).Data);
                    id.IsRequestingPiecesCount--;
                }
                else
                    PeerIO.EnqueueSendMessage (id.Connection, id.Encryptor, message, null, id.Monitor, id.TorrentManager.Monitor, endSendMessageCallback, id);
            }
            catch (Exception ex)
            {
                CleanupSocket(id, ex.Message);
            }
        }

        #endregion


        #region Methods

        internal void AsyncCleanupSocket(PeerId id, bool localClose, string message)
        {
            if (id == null) // Sometimes onEncryptoError will fire with a null id
                return;

            try
            {
                // It's possible the peer could be in an async send *and* receive and so end up
                // in this block twice. This check makes sure we don't try to double dispose.
                if (id.Connection == null)
                    return;

                // We can reuse this peer if the connection says so and it's not marked as inactive
                bool canResuse = id.Connection.CanReconnect && !id.TorrentManager.InactivePeerManager.InactivePeerList.Contains(id.Uri);
                Logger.Log(id.Connection, "Cleanup Reason : " + message);

                Logger.Log(id.Connection, "*******Cleaning up*******");
                id.TorrentManager.PieceManager.Picker.CancelRequests(id);
                id.Peer.CleanedUpCount++;

                if (id.PeerExchangeManager != null)
                    id.PeerExchangeManager.Dispose();

                if (!id.AmChoking)
                    id.TorrentManager.UploadingTo--;

                id.Connection.Dispose();
                id.Connection = null;

                id.TorrentManager.Peers.ConnectedPeers.RemoveAll(delegate(PeerId other) { return id == other; });

                if (id.TorrentManager.Peers.ActivePeers.Contains(id.Peer))
                    id.TorrentManager.Peers.ActivePeers.Remove(id.Peer);

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canResuse && id.Peer.PeerId != engine.PeerId)
                {
                    if (!id.TorrentManager.Peers.AvailablePeers.Contains(id.Peer) && id.Peer.CleanedUpCount < 5)
                        id.TorrentManager.Peers.AvailablePeers.Insert(0, id.Peer);
                }
            }

            finally
            {
                id.TorrentManager.RaisePeerDisconnected(
                    new PeerConnectionEventArgs( id.TorrentManager, id, Direction.None, message ) );
            }
        }

        internal void CancelPendingConnects(TorrentManager manager)
        {
            foreach (AsyncConnectState c in pendingConnects)
                if (c.Manager == manager)
                    c.Connection.Dispose();
        }

        /// <summary>
        /// This method is called when a connection needs to be closed and the resources for it released.
        /// </summary>
        /// <param name="id">The peer whose connection needs to be closed</param>
        internal void CleanupSocket(PeerId id, string message)
        {
            ClientEngine.MainLoop.Queue(delegate {
                id.ConnectionManager.AsyncCleanupSocket(id, true, message);
            });
        }


        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="result"></param>
        private void IncomingConnectionAccepted(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;

            try
            {
                if (!succeeded) {
                    var args = new PeerConnectionFailedEventArgs(id.TorrentManager, id.Peer, Direction.Incoming, "Incoming connection coult not be accepted");
                    id.TorrentManager.RaiseConnectionAttemptFailed (args);
                }

                bool maxAlreadyOpen = OpenConnections >= Math.Min(this.MaxOpenConnections, id.TorrentManager.Settings.MaxConnections);
                if (!succeeded || id.Peer.PeerId == engine.PeerId || maxAlreadyOpen)
                {
                    CleanupSocket (id, "Connection was not accepted");
                    return;
                }

                if (id.TorrentManager.Peers.ActivePeers.Contains(id.Peer))
                {
                    Logger.Log(id.Connection, "ConnectionManager - Already connected to peer");
                    id.Connection.Dispose();
                    return;
                }

                Logger.Log(id.Connection, "ConnectionManager - Incoming connection fully accepted");
                id.TorrentManager.Peers.AvailablePeers.Remove(id.Peer);
                id.TorrentManager.Peers.ActivePeers.Add(id.Peer);
                id.TorrentManager.Peers.ConnectedPeers.Add(id);
                id.WhenConnected = DateTime.Now;
                // Baseline the time the last block was received
                id.LastBlockReceived = DateTime.Now;

                id.TorrentManager.HandlePeerConnected(id, Direction.Incoming);

                // We've sent our handshake so begin our looping to receive incoming message
                PeerIO.EnqueueReceiveMessage (id.Connection, id.Decryptor, id.TorrentManager.DownloadLimiter, id.Monitor, id.TorrentManager, messageReceivedCallback, id);
            }
            catch (Exception e)
            {
                CleanupSocket (id, e.Message);
            }
        }



        private void MessageReceived (bool successful, PeerMessage message, object state)
        {
            PeerId id = (PeerId) state;
            if (!successful)
            {
                id.ConnectionManager.CleanupSocket (id, "Could not receive a message");
                return;
            }

            try
            {
                PeerMessageEventArgs e = new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)message, Direction.Incoming, id);
                id.ConnectionManager.RaisePeerMessageTransferred(e);

                message.Handle(id);

                id.LastMessageReceived = DateTime.Now;
                PeerIO.EnqueueReceiveMessage (id.Connection, id.Decryptor, id.TorrentManager.DownloadLimiter, id.Monitor, id.TorrentManager, messageReceivedCallback, id);
            }
            catch (TorrentException ex)
            {
                id.ConnectionManager.CleanupSocket (id, ex.Message);
            }
        }

        /// <param name="id">The peer whose message queue you want to start processing</param>
        internal void ProcessQueue(PeerId id)
        {
            if (id.QueueLength == 0)
            {
                id.ProcessingQueue = false;
                return;
            }

            PeerMessage msg = id.Dequeue();
            if (msg is PieceMessage)
            {
                using (ManualResetEvent handle = new ManualResetEvent(false)) {
                    PieceMessage pm = (PieceMessage)msg;
                    pm.Data = BufferManager.EmptyBuffer;
                    ClientEngine.BufferManager.GetBuffer(ref pm.Data, pm.ByteLength);
                    engine.DiskManager.QueueRead(id.TorrentManager, pm.StartOffset + ((long) pm.PieceIndex * id.TorrentManager.Torrent.PieceLength), pm.Data, pm.RequestLength, delegate
                    {
                        handle.Set();
                    });
                    handle.WaitOne();
                    id.PiecesSent++;
                }
            }
            try
            {
                SendMessage(id, msg, this.messageSentCallback);
            }
            catch (Exception e)
            {
                CleanupSocket(id, "Exception calling SendMessage: " + e.Message);
            }
        }

        internal void RaisePeerMessageTransferred(PeerMessageEventArgs e)
        {
            if (PeerMessageTransferred == null)
                return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                EventHandler<PeerMessageEventArgs> h = PeerMessageTransferred;
                if (h == null)
                    return;

                if (!(e.Message is MessageBundle))
                {
                    h(e.TorrentManager, e);
                }
                else
                {
                    // Message bundles are only a convience for internal usage!
                    MessageBundle b = (MessageBundle)e.Message;
                    foreach (PeerMessage message in b.Messages)
                    {
                        PeerMessageEventArgs args = new PeerMessageEventArgs(e.TorrentManager, message, e.Direction, e.ID);
                        h(args.TorrentManager, args);
                    }
                }
            });
        }

        internal bool ShouldBanPeer(Peer peer)
        {
            if (BanPeer == null)
                return false;

            AttemptConnectionEventArgs e = new AttemptConnectionEventArgs(peer);
            BanPeer(this, e);
            return e.BanPeer;
        }

        internal void TryConnect()
        {
            TorrentManager m = null;
            
            // If we have already reached our max connections globally, don't try to connect to a new peer
            while (OpenConnections < this.MaxOpenConnections && this.HalfOpenConnections < this.MaxHalfOpenConnections) {
                // Check each torrent manager in turn to see if they have any peers we want to connect to
                for (int i = TryConnectIndex; i <  engine.Torrents.Count; i ++) {
                    if (TryConnect (engine.Torrents [i])) {
                        TryConnectIndex = (i + 1) % engine.Torrents.Count;
                        continue;
                    }
                }

                TryConnectIndex = 0;
                break;
            }
        }
        
        bool TryConnect (TorrentManager manager)
        {
            int i;
            Peer peer;
            if (!manager.Mode.CanAcceptConnections)
                return false;
            
            // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
            if (manager.Peers.ConnectedPeers.Count >= manager.Settings.MaxConnections)
                return false;
            
            // If the torrent isn't active, don't connect to a peer for it
            if (!manager.Mode.CanAcceptConnections)
                return false;
            
            // If we are not seeding, we can connect to anyone. If we are seeding, we should only connect to a peer
            // if they are not a seeder.
            for (i = 0; i < manager.Peers.AvailablePeers.Count; i++)
                if (manager.Mode.ShouldConnect (manager.Peers.AvailablePeers[i]))
                    break;
            
            // If this is true, there were no peers in the available list to connect to.
            if (i == manager.Peers.AvailablePeers.Count)
                return false;
            
            // Remove the peer from the lists so we can start connecting to him
            peer = manager.Peers.AvailablePeers[i];
            manager.Peers.AvailablePeers.RemoveAt(i);

            // Do not try to connect to ourselves
            if (peer.ConnectionUri.Port == manager.Engine.Listener.Endpoint.Port)
            {
                if (manager.Engine.Listener.Endpoint.Address.ToString() == peer.ConnectionUri.Host)
                    return false;

                if (manager.Engine.Listener.Endpoint.Address == IPAddress.Any)
                    foreach (var intf in NetworkInterface.GetAllNetworkInterfaces())
                        if (intf.OperationalStatus == OperationalStatus.Up)
                            foreach (var ip in intf.GetIPProperties().UnicastAddresses)
                                if (ip.Address.ToString() == peer.ConnectionUri.Host)
                                    return false;
            }

            if (ShouldBanPeer(peer))
                return false;
            
            // Connect to the peer
            ConnectToPeer(manager, peer);
            return true;
        }

        #endregion
    }
}
