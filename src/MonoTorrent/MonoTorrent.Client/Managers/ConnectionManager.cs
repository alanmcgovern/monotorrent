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
using MonoTorrent.Client.Tasks;

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
        public const int ChunkLength = 2096;   // Download in 2kB chunks to allow for better rate limiting

        // Create the callbacks and reuse them. Reduces ongoing allocations by a fair few megs
        private MessagingCallback bitfieldSentCallback;
        private MessagingCallback handshakeReceievedCallback;
        private MessagingCallback handshakeSentCallback;
        private MessagingCallback messageLengthReceivedCallback;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;

        private AsyncCallback endCheckEncryptionCallback;
        private AsyncConnect endCreateConnectionCallback;
        private AsyncTransfer incomingConnectionAcceptedCallback;
        private AsyncTransfer onEndReceiveMessageCallback;
        private AsyncTransfer onEndSendMessageCallback;

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
                DelegateTask task = new DelegateTask(delegate {
                    return Toolbox.Accumulate<TorrentManager>(torrents, delegate(TorrentManager m) {
                        return m.Peers.ConnectedPeers.Count;
                    });
                });
                ClientEngine.MainLoop.QueueWait(delegate { task.Execute(); });
                return (int)task.Result;
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
            this.onEndReceiveMessageCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { EndReceiveMessage(s, c, o); }); };
            this.onEndSendMessageCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { EndSendMessage(s, c, o); }); };
            this.bitfieldSentCallback = new MessagingCallback(this.onPeerBitfieldSent);
            this.endCreateConnectionCallback = delegate(bool succeeded, object state) { ClientEngine.MainLoop.Queue(delegate { EndCreateConnection(succeeded, state); }); };
            this.handshakeSentCallback = new MessagingCallback(this.onPeerHandshakeSent);
            this.handshakeReceievedCallback = new MessagingCallback(this.onPeerHandshakeReceived);
            this.incomingConnectionAcceptedCallback = delegate(bool s, int c, object o) { ClientEngine.MainLoop.Queue(delegate { IncomingConnectionAccepted(s, c, o); }); };
            this.messageLengthReceivedCallback = new MessagingCallback(this.onPeerMessageLengthReceived);
            this.messageReceivedCallback = new MessagingCallback(this.onPeerMessageReceived);
            this.messageSentCallback = new MessagingCallback(this.onPeerMessageSent);
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

            manager.Peers.ConnectingToPeers.Add(peer);

            peer.LastConnectionAttempt = DateTime.Now;
            AsyncConnectState c = new AsyncConnectState(manager, peer, connection, endCreateConnectionCallback);
            try
            {
                NetworkIO.EnqueueConnect(c);
            }
            catch (Exception)
            {
                // If there's a socket exception at this point, just drop the peer's details silently
                // as they must be invalid.
                manager.Peers.ConnectingToPeers.Remove(peer);
            }

            // Try to connect to another peer
            TryConnect();
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we try to create a remote connection
        /// </summary>
        /// <param name="result"></param>
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

                // Increase the count of the "open" connections
                EncryptorFactory.BeginCheckEncryption(id, this.endCheckEncryptionCallback, id);
                
                id.TorrentManager.Peers.ConnectedPeers.Add(id);
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


        private void EndReceiveMessage(bool succeeded, int count, object state)
        {
            string reason = null;
            bool cleanUp = false;
            PeerId id = (PeerId)state;

            try
            {
                // If the connection is null, just return
                if (id.Connection == null)
                    return;

                // If we receive 0 bytes, the connection has been closed, so exit
                if (!succeeded)
                    throw new SocketException((int)SocketError.SocketError);

                int bytesReceived = count;
                if (bytesReceived == 0)
                {
                    reason = "EndReceiveMessage: Received zero bytes";
                    Logger.Log(id.Connection, "ConnectionManager - Received zero bytes instead of message");
                    cleanUp = true;
                    return;
                }

                // If the first byte is '7' and we're receiving more than 256 bytes (a made up number)
                // then this is a piece message, so we add it as "data", not protocol. 256 bytes should filter out
                // any non piece messages that happen to have '7' as the first byte.
                // The We need to skip past the first 4 bytes, they are message length
                TransferType type = (id.recieveBuffer.Array[id.recieveBuffer.Offset + 4] == PieceMessage.MessageId && id.BytesToRecieve > 256) ? TransferType.Data : TransferType.Protocol;
                id.ReceivedBytes(bytesReceived, type);
                id.TorrentManager.Monitor.BytesReceived(bytesReceived, type);

                // If we don't have the entire message, recieve the rest
                if (id.BytesReceived < id.BytesToRecieve)
                {
                    id.TorrentManager.downloadQueue.Add(id);
                    id.TorrentManager.ResumePeers();
                    return;
                }
                else
                {
                    // Invoke the callback we were told to invoke once the message had been received fully
                    ArraySegment<byte> b = id.recieveBuffer;
                    if (id.MessageReceivedCallback == messageLengthReceivedCallback)
                        id.Decryptor.Decrypt(b.Array, b.Offset, id.BytesToRecieve);
                    else
                        id.Decryptor.Decrypt(b.Array, b.Offset + 4, id.BytesToRecieve - 4);
                    id.MessageReceivedCallback(id);
                }
            }

            catch (Exception ex)
            {
                Logger.Log(id.Connection, "Exception recieving message" + ex.ToString());
                reason = String.Format("Exception receiving: {0} - {1}", ex.ToString(), ex.Message);
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, reason);
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

                int bytesSent = count;
                if (bytesSent == 0)
                {
                    reason = "Sending error: Sent zero bytes";
                    Logger.Log(id.Connection, "ConnectionManager - Sent zero bytes when sending a message");
                    cleanup = true;
                    return;
                }

                // Log the data sent in both the peers and torrentmangers connection monitors
                TransferType type = (id.CurrentlySendingMessage is PieceMessage) ? TransferType.Data : TransferType.Protocol;
                id.SentBytes(bytesSent, type);
                id.TorrentManager.Monitor.BytesSent(bytesSent, type);

                // If we havn't sent everything, send the rest of the data
                if (id.BytesSent != id.BytesToSend)
                {
                    id.TorrentManager.uploadQueue.Add(id);
                    id.TorrentManager.ResumePeers();
                    return;
                }
                else
                {
                    // Invoke the callback which we were told to invoke after we sent this message
                    id.MessageSentCallback(id);
                }
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


        private void onPeerHandshakeSent(PeerId id)
        {
            id.TorrentManager.RaisePeerConnected(new PeerConnectionEventArgs(id.TorrentManager, id, Direction.Outgoing));

            Logger.Log(id.Connection, "ConnectionManager - Sent Handshake");

            // Receive the handshake
            // FIXME: Will fail if protocol version changes. FIX THIS
            //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
            ReceiveMessage(id, 68, this.handshakeReceievedCallback);
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer handshake
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(PeerId id)
        {
            string reason = null;
            bool cleanUp = false;
            PeerMessage msg;

            try
            {
                // If the connection is closed, just return
                if (id.Connection == null)
                    return;

                // Decode the handshake and handle it
                msg = new HandshakeMessage();
                msg.Decode(id.recieveBuffer, 4, id.BytesToRecieve - 4);
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void onPeerBitfieldSent(PeerId id)
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
            id.ProcessingQueue = false;

            ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message length
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageLengthReceived(PeerId id)
        {
            // If the connection is null, we just return
            if (id.Connection == null)
                return;

            Logger.Log(id.Connection, "ConnectionManager - Recieved message length");

            // Decode the message length from the buffer. It is a big endian integer, so make sure
            // it is converted to host endianness.
            int messageBodyLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(id.recieveBuffer.Array, id.recieveBuffer.Offset));

            // Free the existing receive buffer and then get a new one which can
            // contain the amount of bytes we need to receive.
            //ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);

            // If bytes to receive is zero, it means we received a keep alive message
            // so we just start receiving a new message length again
            if (messageBodyLength == 0)
            {
                Logger.Log(id.Connection, "ConnectionManager - Received keepalive");
                id.LastMessageReceived = DateTime.Now;
                ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
            }

            // Otherwise queue the peer in the Receive buffer and try to resume downloading off him
            else
            {
                Logger.Log(id.Connection, "Recieving message: {0} bytes", messageBodyLength.ToString());
                ReceiveMessage(id, messageBodyLength, this.messageReceivedCallback);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageReceived(PeerId id)
        {
            string reason = null;
            bool cleanUp = false;

            try
            {
                if (id.Connection == null)
                    return;

                MessageHandler.EnqueueReceived(id, id.recieveBuffer, 0, id.BytesToRecieve);

                //FIXME: I thought i was using 5 (i changed the check below from 3 to 5)...
                // if the peer has sent us three bad pieces, we close the connection.
                if (id.Peer.TotalHashFails == 5)
                {
                    reason = "3 hashfails";
                    Logger.Log(id.Connection, "ConnectionManager - 5 hashfails");
                    cleanUp = true;
                    return;
                }

                id.LastMessageReceived = DateTime.Now;

                // Free the large buffer used to recieve the piece message and get a small buffer
                //ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);

                ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
            }
            catch (TorrentException ex)
            {
                reason = ex.Message;
                Logger.Log(id.Connection, "Invalid message recieved: {0}", ex.Message);
                cleanUp = true;
                return;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, true, reason);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when a peer message is sent
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageSent(PeerId id)
        {
            // If the peer has been cleaned up, just return.
            if (id.Connection == null)
                return;

            // Fire the event to let the user know a message was sent
            RaisePeerMessageTransferred(new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)id.CurrentlySendingMessage, Direction.Outgoing, id));

            //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
            Logger.Log(id.Connection, "ConnectionManager - Sent message: " + id.CurrentlySendingMessage.ToString());
            id.LastMessageSent = DateTime.Now;
            this.ProcessQueue(id);
        }


        /// <summary>
        /// Receives exactly length number of bytes from the specified peer connection and invokes the supplied callback if successful
        /// </summary>
        /// <param name="id">The peer to receive the message from</param>
        /// <param name="length">The length of the message to receive</param>
        /// <param name="callback">The callback to invoke when the message has been received</param>
        private void ReceiveMessage(PeerId id, int length, MessagingCallback callback)
        {
            ArraySegment<byte> newBuffer = BufferManager.EmptyBuffer;
            bool cleanUp = false;
            try
            {
                if (id.Connection == null)
                    return;

                if (length > RequestMessage.MaxSize)
                {
                    Logger.Log(id.Connection, "Tried to send too much data: {0} bytes", length.ToString());
                    cleanUp = true;
                    return;
                }

                int alreadyReceived = (id.BytesReceived) - id.BytesToRecieve;
                if (callback == messageLengthReceivedCallback)
                    ClientEngine.BufferManager.GetBuffer(ref newBuffer, Math.Max(alreadyReceived, length));
                else
                    ClientEngine.BufferManager.GetBuffer(ref newBuffer, Math.Max(alreadyReceived, length + 4));

                // Prepend the length
                Message.Write(newBuffer.Array, newBuffer.Offset, length);

                // Copy the extra data from the old buffer into the new buffer.
                ArraySegment<byte> oldBuffer = id.recieveBuffer;

                if (callback == messageLengthReceivedCallback)
                    Message.Write(newBuffer.Array, newBuffer.Offset, oldBuffer.Array, oldBuffer.Offset + id.BytesToRecieve, alreadyReceived);
                else
                    Message.Write(newBuffer.Array, newBuffer.Offset + 4, oldBuffer.Array, oldBuffer.Offset + id.BytesToRecieve, alreadyReceived);

                // Free the old buffer and set the new buffer
                ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                id.recieveBuffer = newBuffer;

                if (callback == messageLengthReceivedCallback)
                {
                    id.BytesReceived = alreadyReceived;
                    id.BytesToRecieve = length;
                }
                else
                {
                    id.BytesReceived = alreadyReceived + 4;
                    id.BytesToRecieve = length + 4;
                }

                id.MessageReceivedCallback = callback;

                if (alreadyReceived < length)
                {
                    id.TorrentManager.downloadQueue.Add(id);
                    id.TorrentManager.ResumePeers();
                }
                else
                {
                    id.MessageReceivedCallback(id);
                }
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ConnectionManager - Socket error receiving message");
                cleanUp = true;
            }
            finally
            {
                if(cleanUp)
                    CleanupSocket(id, "Couldn't Receive Message");

            }
        }


        /// <summary>
        /// Sends the specified message to the specified peer and invokes the supplied callback if successful
        /// </summary>
        /// <param name="id">The peer to send the message to</param>
        /// <param name="message">The  message to send</param>
        /// <param name="callback">The callback to invoke when the message has been sent</param>
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

                id.TorrentManager.uploadQueue.Add(id);
                id.TorrentManager.ResumePeers();
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

                bool canResuse = id.Connection.CanReconnect;
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

                id.TorrentManager.uploadQueue.RemoveAll(delegate(PeerId other) { return id == other; });
                id.TorrentManager.downloadQueue.RemoveAll(delegate(PeerId other) { return id == other; });
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
            CleanupSocket(id, true, message);
        }


        internal void CleanupSocket(PeerId id, bool localClose, string message)
        {
            id.DisconnectReason = message;
            MessageHandler.EnqueueCleanup(id);
        }


        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="result"></param>
        internal void IncomingConnectionAccepted(bool succeeded, int count, object state)
        {
            string reason = null;
            int bytesSent;
            bool cleanUp = false;
            PeerId id = (PeerId)state;

            try
            {
                if (!succeeded)
                {
                    cleanUp = true;
                    return;
                }
                bytesSent = count;
                id.BytesSent += bytesSent;
                if (bytesSent != id.BytesToSend)
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
                Logger.Log(id.Connection, "ConnectionManager - Recieving message length");
                ClientEngine.BufferManager.GetBuffer(ref id.recieveBuffer, 68);
                ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
            }
            catch (Exception e)
            {
                reason = String.Format("Exception for incoming connection: {0} - {1}", e.ToString(), e.Message);
                Logger.Log(id.Connection, "Socket exception when accepting peer");
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


        internal void onEncryptorReady(PeerId id)
        {
            try
            {
                //ClientEngine.BufferManager.FreeBuffer(ref id.sendBuffer);
                ReceiveMessage(id, 68, this.handshakeReceievedCallback);
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id, "Null Ref for encryptor");
            }
            catch (Exception)
            {
                CleanupSocket(id, "Exception on encryptor");
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

            id.ProcessingQueue = true;
            try
            {
                SendMessage(id, msg, this.messageSentCallback);
                Logger.Log(id.Connection, "ConnectionManager - Sending message from queue: {0}", msg.ToString());
            }
            catch (Exception e)
            {
                Logger.Log(id.Connection, "ConnectionManager - Socket exception dequeuing message");
                CleanupSocket(id, String.Format("Exception calling SendMessage: {0} - {1}", e.ToString(), e.Message));
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


        /// <summary>
        /// Makes a peer start downloading/uploading
        /// </summary>
        /// <param name="id">The peer to resume</param>
        /// <param name="downloading">True if you want to resume downloading, false if you want to resume uploading</param>
        internal int ResumePeer(PeerId id, bool downloading)
        {
            // We attempt to send/receive 'bytecount' number of bytes
            int byteCount;
            bool cleanUp = false;

            try
            {
                if (id.Connection == null)
                {
                    cleanUp = true;
                    return 0;
                }
                if (downloading)
                {
                    byteCount = (id.BytesToRecieve - id.BytesReceived) > ChunkLength ? ChunkLength : id.BytesToRecieve - id.BytesReceived;
                    NetworkIO.EnqueueReceive(id.Connection, id.recieveBuffer, id.BytesReceived, byteCount, onEndReceiveMessageCallback, id);
                }
                else
                {
                    byteCount = (id.BytesToSend - id.BytesSent) > ChunkLength ? ChunkLength : (id.BytesToSend - id.BytesSent);
                    NetworkIO.EnqueueSend(id.Connection, id.sendBuffer, id.BytesSent, byteCount, this.onEndSendMessageCallback, id);
                }

                return byteCount;
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ConnectionManager - SocketException resuming");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, "Exception resuming");
            }
            return 0;
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
