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
        private MessagingCallback bitfieldSentCallback;
        private AsyncTransfer handshakeReceievedCallback;
        private MessagingCallback handshakeSentCallback;
        private MessagingCallback messageLengthReceivedCallback;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;

        private AsyncCallback endCheckEncryptionCallback;
        private AsyncConnect endCreateConnectionCallback;
        internal AsyncTransfer incomingConnectionAcceptedCallback;
        private AsyncTransfer endReceiveMessageCallback;
        private AsyncTransfer endSendMessageCallback;

        private MonoTorrentCollection<TorrentManager> torrents;

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
                return (int)Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) {
                    return m.Peers.ConnectedPeers.Count;
                });
            }
        }


        /// <summary>
        /// The maximum number of open connections
        /// </summary>
        public int MaxOpenConnections
        {
            get { return this.engine.Settings.GlobalMaxConnections; }
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

            this.endCheckEncryptionCallback = delegate(IAsyncResult result) { ClientEngine.MainLoop.Queue(delegate { EndCheckEncryption(result); }); };
            this.endSendMessageCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { EndSendMessage(s, c, o); }); };
            this.endCreateConnectionCallback = delegate(bool succeeded, object state) { ClientEngine.MainLoop.Queue(delegate { EndCreateConnection(succeeded, state); }); };
            this.incomingConnectionAcceptedCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { IncomingConnectionAccepted(s, c, o); }); };

            this.bitfieldSentCallback = new MessagingCallback(PeerBitfieldSent);
            this.handshakeSentCallback = new MessagingCallback(this.PeerHandshakeSent);
            this.handshakeReceievedCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { PeerHandshakeReceived(s, c, o); }); };
            this.messageSentCallback = new MessagingCallback(this.PeerMessageSent);

            this.torrents = new MonoTorrentCollection<TorrentManager>();
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
            AsyncConnectState c = new AsyncConnectState(manager, peer, connection, endCreateConnectionCallback);

            manager.Peers.ConnectingToPeers.Add(peer);
            NetworkIO.EnqueueConnect(c);

            // Try to connect to another peer
            TryConnect();
        }

        private void EndCreateConnection(bool succeeded, object state)
        {
            AsyncConnectState connect = (AsyncConnectState)state;
            if(connect.Manager.State != TorrentState.Downloading && connect.Manager.State != TorrentState.Seeding)
                connect.Connection.Dispose();
            
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
            bool cleanUp = false;
            string reason = null;

            try
            {
                // If we have too many open connections, close the connection
                if (OpenConnections > this.MaxOpenConnections)
                {
                    Logger.Log(id.Connection, "ConnectionManager - Too many connections");
                    reason = "Too many connections";
                    cleanUp = true;
                    return;
                }

                id.ProcessingQueue = true;
                // Increase the count of the "open" connections
                EncryptorFactory.BeginCheckEncryption(id, this.endCheckEncryptionCallback, id);
                
                id.TorrentManager.Peers.ConnectedPeers.Add(id);
				id.WhenConnected = DateTime.Now;
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "failed to encrypt");

                id.TorrentManager.RaiseConnectionAttemptFailed(
                    new PeerConnectionFailedEventArgs(id.TorrentManager, id.Peer, Direction.Outgoing, "ProcessFreshConnection: failed to encrypt"));

                id.Connection.Dispose();
                id.Connection = null;
            }
            finally
            {
                // Decrement the half open connections
                if (cleanUp)
                    CleanupSocket(id, reason);
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
                {
                    Console.WriteLine("What is this initial data?!");
                    throw new EncryptionException("unhandled initial data");
                }

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
                    HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
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
            string reason = null;
            bool cleanup = false;
            PeerId id = (PeerId)state;

            try
            {
                // If the peer has disconnected, don't continue
                if (id.Connection == null)
                    return;

                // If we have sent zero bytes, that is a sign the connection has been closed
                if (!succeeded)
                    throw new SocketException((int)SocketError.SocketError);

                id.BytesSent += count;
                // Invoke the callback which we were told to invoke after we sent this message
                id.MessageSentCallback(id);
            }
            catch (Exception)
            {
                reason = "Exception EndSending";
                Logger.Log(id.Connection, "ConnectionManager - Socket exception sending message");
                cleanup = true;
            }
            finally
            {
                if (cleanup)
                    CleanupSocket(id, reason);
            }
        }

        private void PeerHandshakeSent(PeerId id)
        {
            id.TorrentManager.RaisePeerConnected(new PeerConnectionEventArgs(id.TorrentManager, id, Direction.Outgoing));

            Logger.Log(id.Connection, "ConnectionManager - Sent Handshake");

            // Receive the handshake
            // FIXME: Will fail if protocol version changes. FIX THIS
            //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
            ClientEngine.BufferManager.GetBuffer(ref id.recieveBuffer, 68);
            NetworkIO.EnqueueReceive(id.Connection, id.recieveBuffer, 0, 68, handshakeReceievedCallback, id);
        }

        private void PeerHandshakeReceived(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;
            string reason = null;
            bool cleanUp = false;
            PeerMessage msg;

            try
            {
                // If the connection is closed, just return
                if (!succeeded)
                    return;

                // Decode the handshake and handle it
                id.Decryptor.Decrypt(id.recieveBuffer.Array, id.recieveBuffer.Offset, count);
                msg = new HandshakeMessage();
                msg.Decode(id.recieveBuffer, 0, count);
                msg.Handle(id);

                Logger.Log(id.Connection, "ConnectionManager - Handshake recieved");
                if (id.SupportsFastPeer && ClientEngine.SupportsFastPeer)
                {
                    if (id.TorrentManager.Bitfield.AllFalse || id.TorrentManager.IsInitialSeeding)
                        msg = new HaveNoneMessage();

                    else if (id.TorrentManager.Bitfield.AllTrue)
                        msg = new HaveAllMessage();

                    else
                        msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                }
                else if (id.TorrentManager.IsInitialSeeding)
                {
                    BitField btfld = new BitField(id.TorrentManager.Bitfield.Length);
                    btfld.SetAll(false);
                    msg = new BitfieldMessage(btfld);
                }
                else
                {
                    msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                }

                if (id.SupportsLTMessages)
                {
                    MessageBundle bundle = new MessageBundle();
                    bundle.Messages.Add(new ExtendedHandshakeMessage());
                    bundle.Messages.Add(msg);
                    msg = bundle;
                }

                //ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                SendMessage(id, msg, this.bitfieldSentCallback);
            }
            catch (TorrentException)
            {
                Logger.Log(id.Connection, "ConnectionManager - Couldn't decode the message");
                reason = "Couldn't decode handshake";
                cleanUp = true;
                return;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, reason);
            }
        }

        private void PeerBitfieldSent(PeerId id)
        {
            if (id.Connection == null)
                return;

            // Free the old buffer and get a new one to recieve the length of the next message
            //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);

            // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
            // even if they are choked
            if (ClientEngine.SupportsFastPeer && id.SupportsFastPeer)
                for (int i = 0; i < id.AmAllowedFastPieces.Count; i++)
                    id.Enqueue(new AllowedFastMessage(id.AmAllowedFastPieces[i]));

            // Allow the auto processing of the send queue to commence
            if (id.QueueLength > 0)
                id.ConnectionManager.ProcessQueue(id);
            else
                id.ProcessingQueue = false;

            // Begin the infinite looping to receive messages
            ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
            NetworkIO.ReceiveMessage(id);
        }

        private void PeerMessageSent(PeerId id)
        {
            // If the peer has been cleaned up, just return.
            if (id.Connection == null)
                return;

            // Fire the event to let the user know a message was sent
            RaisePeerMessageTransferred(new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)id.CurrentlySendingMessage, Direction.Outgoing, id));

            //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
            id.LastMessageSent = DateTime.Now;
            this.ProcessQueue(id);
        }

        private void SendMessage(PeerId id, PeerMessage message, MessagingCallback callback)
        {
            bool cleanup = false;

            try
            {
                if (id.Connection == null)
                    return;
                ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
                ClientEngine.BufferManager.GetBuffer(ref id.sendBuffer, message.ByteLength);
                id.MessageSentCallback = callback;
                id.CurrentlySendingMessage = message;
                if (message is PieceMessage)
                    id.IsRequestingPiecesCount--;

                id.BytesSent = 0;
                id.BytesToSend = message.Encode(id.sendBuffer, 0);
                id.Encryptor.Encrypt(id.sendBuffer.Array, id.sendBuffer.Offset, id.BytesToSend);

                RateLimiter limiter = engine.Settings.GlobalMaxUploadSpeed > 0 ? engine.uploadLimiter : null;
                limiter = limiter ?? (id.TorrentManager.Settings.MaxUploadSpeed > 0 ? id.TorrentManager.uploadLimiter : null);
                NetworkIO.EnqueueSend(id.Connection, id.sendBuffer, id.BytesSent, id.BytesToSend, endSendMessageCallback, id, limiter, id.TorrentManager.Monitor, id.Monitor);
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ConnectionManager - Socket error sending message");
                cleanup = true;
            }
            finally
            {
                if (cleanup)
                    CleanupSocket(id, "Couldn't SendMessage");
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
                bool canResuse = id.Connection.CanReconnect && !id.TorrentManager.InactivePeerManager.InactiveUris.Contains(id.Uri);
                Logger.Log(id.Connection, "Cleanup Reason : " + message);

                Logger.Log(id.Connection, "*******Cleaning up*******");
                id.TorrentManager.PieceManager.RemoveRequests(id);
                id.Peer.CleanedUpCount++;

                if (id.PeerExchangeManager != null)
                    id.PeerExchangeManager.Dispose();

                ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
                ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);

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
                TryConnect();
            }
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
            string reason = null;
            bool cleanUp = false;
            PeerId id = (PeerId)state;

            try
            {
                if (!succeeded)
                {
                    cleanUp = true;
                    return;
                }
                id.BytesSent += count;
                if (count != id.BytesToSend)
                {
                    NetworkIO.EnqueueSend(id.Connection, id.sendBuffer, id.BytesSent,
                                          id.BytesToSend - id.BytesSent, incomingConnectionAcceptedCallback, id);
                    return;
                }

                if (id.Peer.PeerId == engine.PeerId) // The tracker gave us our own IP/Port combination
                {
                    Logger.Log(id.Connection, "ConnectionManager - Recieved myself");
                    reason = "Received myself";
                    cleanUp = true;
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

                //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);

                id.TorrentManager.RaisePeerConnected(new PeerConnectionEventArgs(id.TorrentManager, id, Direction.Incoming));

                if (OpenConnections >= Math.Min(this.MaxOpenConnections, id.TorrentManager.Settings.MaxConnections))
                {
                    reason = "Too many peers";
                    cleanUp = true;
                    return;
                }
                if (id.TorrentManager.IsInitialSeeding)
                {
                    int pieceIndex = id.TorrentManager.InitialSeed.GetNextPieceForPeer(id);
                    if (pieceIndex != -1)
                    {
                        // If the peer has the piece already, we need to recalculate his "interesting" status.
                        bool hasPiece = id.BitField[pieceIndex];

                        // Check to see if have supression is enabled and send the have message accordingly
                        if (!hasPiece || (hasPiece && !this.engine.Settings.HaveSupressionEnabled))
                            id.Enqueue(new HaveMessage(pieceIndex));
                    }
                }
                foreach (int piece in id.AmAllowedFastPieces)
                    id.Enqueue(new AllowedFastMessage(piece));
                Logger.Log(id.Connection, "ConnectionManager - Recieving message length");
                ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                NetworkIO.ReceiveMessage(id);
            }
            catch (Exception e)
            {
                reason = "Exception for incoming connection: {0}" + e.Message;
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                {
                    CleanupSocket(id, reason);

                    id.TorrentManager.RaiseConnectionAttemptFailed(
                        new PeerConnectionFailedEventArgs(id.TorrentManager, id.Peer, Direction.Incoming, reason));
                }
            }
        }


        /// <summary>
        /// This method should be called to begin processing messages stored in the SendQueue
        /// </summary>
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
                id.PiecesSent++;

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
            ThreadPool.QueueUserWorkItem(delegate
            {
                EventHandler<PeerMessageEventArgs> h = PeerMessageTransferred;

                if (!(e.Message is MessageBundle))
                {
                    if (h != null)
                        h(e.TorrentManager, e);
                    return;
                }

                // Message bundles are only a convience for internal usage!
                MessageBundle b = (MessageBundle)e.Message;
                foreach (PeerMessage message in b.Messages)
                {
                    PeerMessageEventArgs args = new PeerMessageEventArgs(e.TorrentManager, message, e.Direction, e.ID);
                    if (h != null)
                        h(args.TorrentManager, args);
                }
            });
        }


        internal void RegisterManager(TorrentManager torrentManager)
        {
            if (this.torrents.Contains(torrentManager))
                throw new TorrentException("TorrentManager is already registered in the connection manager");

            this.torrents.Add(torrentManager);
            TryConnect();
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
            try
            {
                int i;
                Peer peer;
                TorrentManager m = null;

                // If we have already reached our max connections globally, don't try to connect to a new peer
                if ((OpenConnections >= this.MaxOpenConnections) || this.HalfOpenConnections >= this.MaxHalfOpenConnections)
                    return;

                // Check each torrent manager in turn to see if they have any peers we want to connect to
                foreach (TorrentManager manager in this.torrents)
                {
                    // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
                    if (manager.Peers.ConnectedPeers.Count >= manager.Settings.MaxConnections)
                        continue;

                    // If the torrent isn't active, don't connect to a peer for it
                    if (manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
                        continue;

                    // If we are not seeding, we can connect to anyone. If we are seeding, we should only connect to a peer
                    // if they are not a seeder.
                    for (i = 0; i < manager.Peers.AvailablePeers.Count; i++)
                        if (manager.State == TorrentState.Seeding && manager.Peers.AvailablePeers[i].IsSeeder)
                            continue;
                        else
                            break;

                    // If this is true, there were no peers in the available list to connect to.
                    if (i == manager.Peers.AvailablePeers.Count)
                        continue;

                    // Remove the peer from the lists so we can start connecting to him
                    peer = manager.Peers.AvailablePeers[i];
                    manager.Peers.AvailablePeers.RemoveAt(i);

                    // Save the manager we're using so we can place it to the end of the list
                    m = manager;

                    if (ShouldBanPeer(peer))
                        return;

                    // Connect to the peer
                    ConnectToPeer(manager, peer);
                    break;
                }

                if (m == null)
                    return;

                // Put the manager at the end of the list so we try the other ones next
                this.torrents.Remove(m);
                this.torrents.Add(m);
            }
            catch (Exception ex)
            {
                engine.RaiseCriticalException(new CriticalExceptionEventArgs(ex, engine));
            }
        }


        internal void UnregisterManager(TorrentManager torrentManager)
        {
            if (!this.torrents.Contains(torrentManager))
                throw new TorrentException("TorrentManager is not registered in the connection manager");

            this.torrents.Remove(torrentManager);
        }

        #endregion

        internal bool IsRegistered(TorrentManager torrentManager)
        {
            return this.torrents.Contains(torrentManager);
        }
    }
}
