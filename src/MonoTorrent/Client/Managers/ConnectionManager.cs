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
using MonoTorrent.Client.PeerMessages;
using System.Net.Sockets;
using System.Threading;
using MonoTorrent.Client.Encryption;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Client
{
    internal delegate void MessagingCallback(PeerId id);

    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        #region Events

        public event EventHandler<PeerConnectionEventArgs> PeerConnected;


        public event EventHandler<PeerConnectionEventArgs> PeerDisconnected;

        /// <summary>
        /// Event that's fired every time a message is sent or Received from a Peer
        /// </summary>
        public event EventHandler<PeerMessageEventArgs> PeerMessageTransferred;

        #endregion


        #region Member Variables
        private ClientEngine engine;
        public const int ChunkLength = 2048;   // Download in 2kB chunks to allow for better rate limiting

        // Create the callbacks and reuse them. Reduces ongoing allocations by a fair few megs
        private MessagingCallback bitfieldSentCallback;
        private MessagingCallback handshakeReceievedCallback;
        private MessagingCallback handshakeSentCallback;
        private MessagingCallback messageLengthReceivedCallback;
        private MessagingCallback messageReceivedCallback;
        private MessagingCallback messageSentCallback;

        private AsyncCallback endCreateConnectionCallback;
        private AsyncCallback incomingConnectionAcceptedCallback;
        private AsyncCallback onEndReceiveMessageCallback;
        private AsyncCallback onEndSendMessageCallback;

        private EncryptorReadyHandler onEncryptorReadyHandler;
        private EncryptorIOErrorHandler onEncryptorIOErrorHandler;
        private EncryptorEncryptionErrorHandler onEncryptorEncryptionErrorHandler;

        private TorrentManagerCollection torrents;

        internal MessageHandler MessageHandler
        {
            get { return this.messageHandler; }
        }
        private MessageHandler messageHandler;
        /// <summary>
        /// The number of half open connections
        /// </summary>
        public int HalfOpenConnections
        {
            get { return this.halfOpenConnections; }
        }
        private int halfOpenConnections;


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
            get { return this.openConnections; }
        }
        private int openConnections;


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

            this.onEndReceiveMessageCallback = new AsyncCallback(EndReceiveMessage);
            this.onEndSendMessageCallback = new AsyncCallback(EndSendMessage);
            this.bitfieldSentCallback = new MessagingCallback(this.onPeerBitfieldSent);
            this.endCreateConnectionCallback = new AsyncCallback(this.EndCreateConnection);
            this.handshakeSentCallback = new MessagingCallback(this.onPeerHandshakeSent);
            this.handshakeReceievedCallback = new MessagingCallback(this.onPeerHandshakeReceived);
            this.incomingConnectionAcceptedCallback = new AsyncCallback(IncomingConnectionAccepted);
            this.messageLengthReceivedCallback = new MessagingCallback(this.onPeerMessageLengthReceived);
            this.messageReceivedCallback = new MessagingCallback(this.onPeerMessageReceived);
            this.messageSentCallback = new MessagingCallback(this.onPeerMessageSent);
            this.torrents = new TorrentManagerCollection();
            this.onEncryptorReadyHandler = new EncryptorReadyHandler(onEncryptorReady);
            this.onEncryptorIOErrorHandler = new EncryptorIOErrorHandler(onEncryptorError);
            this.onEncryptorEncryptionErrorHandler = new EncryptorEncryptionErrorHandler(onEncryptorError);
            this.messageHandler = new MessageHandler();
        }

        #endregion


        #region Async Connection Methods

        internal void ConnectToPeer(TorrentManager manager, PeerId id)
        {
            // Connect to the peer.
            lock (id)
            {
                Logger.Log(id, "Connecting");
                manager.Peers.AddPeer(id, PeerType.Connecting);
                System.Threading.Interlocked.Increment(ref this.halfOpenConnections);

                IEncryptor encryptor;

                if (id.Peer.EncryptionSupported == EncryptionMethods.NoEncryption || !ClientEngine.SupportsEncryption)
                    encryptor = new NoEncryption();
                else
                    encryptor = new PeerAEncryption(manager.Torrent.InfoHash, this.engine.Settings.MinEncryptionLevel);

                encryptor.SetPeerConnectionID(id);
                encryptor.onEncryptorReady += onEncryptorReadyHandler;
                encryptor.onEncryptorIOError += onEncryptorIOErrorHandler;
                encryptor.onEncryptorEncryptionError += onEncryptorEncryptionErrorHandler;

                id.Peer.Connection = new TCPConnection(id.Peer.Location, id.TorrentManager.Torrent.Pieces.Count, encryptor);
                id.Peer.Connection.ProcessingQueue = true;
                id.Peer.LastConnectionAttempt = DateTime.Now;
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                id.Peer.Connection.LastMessageReceived = DateTime.Now;
                id.Peer.Connection.BeginConnect(this.endCreateConnectionCallback, id);
            }

            // Try to connect to another peer
            TryConnect();
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we try to create a remote connection
        /// </summary>
        /// <param name="result"></param>
        private void EndCreateConnection(IAsyncResult result)
        {
            bool fireConnected = false;
            bool cleanUp = false;
            string reason = null;
            PeerId id = (PeerId)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        // If the peer has been cleaned up, then don't continue processing the peer
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null");
                            return;
                        }

                        id.Peer.Connection.EndConnect(result);
                        Logger.Log(id, "Connected");

                        // Remove the peer from the "connecting" list and put them in the "connected" list
                        // because we have now successfully connected to them
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);
                        id.TorrentManager.Peers.AddPeer(id, PeerType.Connected);

                        fireConnected = true;

                        // If we have too many open connections, close the connection
                        if (this.openConnections > this.MaxOpenConnections)
                        {
                            Logger.Log(id, "Too many connections");
                            reason = "Too many connections";
                            cleanUp = true;
                            return;
                        }

                        // Increase the count of the "open" connections
                        System.Threading.Interlocked.Increment(ref this.openConnections);

                        // Create a handshake message to send to the peer
                        HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);

                        if (id.Peer.Connection.Encryptor is NoEncryption || !ClientEngine.SupportsEncryption)
                        {
                            SendMessage(id, handshake, this.handshakeSentCallback);
                        }
                        else
                        {
                            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.SmallMessageBuffer);

                            id.Peer.Connection.BytesSent = 0;
                            id.Peer.Connection.BytesToSend += handshake.Encode(id.Peer.Connection.sendBuffer, 0);

                            // Get a buffer to encode the handshake into, encode the message and send it

                            id.Peer.Connection.Encryptor.AddInitialData(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend);
                            id.Peer.Connection.StartEncryption();
                        }
                    }
                }
            }

            catch (SocketException ex)
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        fireConnected = false;
                        Logger.Log(id, "failed to connect " + ex.Message);
                        id.Peer.FailedConnectionAttempts++;

                        if (id.Peer.Connection != null)
                        {
                            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                            id.Peer.Connection.Dispose();
                        }

                        id.Peer.Connection = null;
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                        if (id.Peer.FailedConnectionAttempts < 2)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        fireConnected = false;
                        Logger.Log(id, "failed to connect " + ex.Message);
                        id.Peer.FailedConnectionAttempts++;

                        if (id.Peer.Connection != null)
                        {
                            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                            id.Peer.Connection.Dispose();
                        }

                        id.Peer.Connection = null;
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                        if (id.Peer.FailedConnectionAttempts < 2)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
                    }
                }
            }
            finally
            {
                if(fireConnected)
                    RaisePeerConnected(new PeerConnectionEventArgs(id, Direction.Outgoing));

                // Decrement the half open connections
                System.Threading.Interlocked.Decrement(ref this.halfOpenConnections);
                if (cleanUp)
                    CleanupSocket(id, reason);

                // Try to connect to another peer
                TryConnect();
            }
        }


        private void EndReceiveMessage(IAsyncResult result)
        {
            string reason = null;
            bool cleanUp = false;
            PeerId id = (PeerId)result.AsyncState;

            try
            {
                lock (id.TorrentManager.resumeLock)
                    lock (id)
                    {
                        // If the connection is null, just return
                        if (id.Peer.Connection == null)
                            return;

                        // If we receive 0 bytes, the connection has been closed, so exit
                        int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || bytesReceived == 0)
                        {
                            reason = "Received zero";
                            Logger.Log(id, "Couldn't receive message");
                            cleanUp = true;
                            return;
                        }

                        // If the first byte is '7' and we're receiving more than 256 bytes (a made up number)
                        // then this is a piece message, so we add it as "data", not protocol. 128 bytes should filter out
                        // any non piece messages that happen to have '7' as the first byte.
                        TransferType type = (id.Peer.Connection.recieveBuffer[0] == 7 && id.Peer.Connection.BytesToRecieve > 256) ? TransferType.Data : TransferType.Protocol;
                        id.Peer.Connection.ReceivedBytes(bytesReceived, type);
                        id.TorrentManager.Monitor.BytesReceived(bytesReceived, type);

                        // If we don't have the entire message, recieve the rest
                        if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                        {
                            id.TorrentManager.Peers.Enqueue(id, PeerType.DownloadQueue);
                            id.TorrentManager.ResumePeers();
                            return;
                        }
                        else
                        {
                            // Invoke the callback we were told to invoke once the message had been received fully
                            id.Peer.Connection.MessageReceivedCallback(id);
                        }
                    }
            }

            catch (SocketException ex)
            {
                Logger.Log(id, "Exception recieving message" + ex.ToString());
                reason = "Socket Exception receiving";
                cleanUp = true;
            }
            catch (ArgumentException ex)
            {
                reason = "FECKIN ARGUMENT EXCEPTIONS!";
                cleanUp = true;
                Logger.Log(id, ex.ToString());
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, reason);
            }
        }


        private void EndSendMessage(IAsyncResult result)
        {
            string reason = null;
            bool cleanup = false;
            PeerId id = (PeerId)result.AsyncState;

            try
            {
                lock (id.TorrentManager.resumeLock)
                    lock (id)
                    {
                        // If the peer has disconnected, don't continue
                        if (id.Peer.Connection == null)
                            return;

                        // If we have sent zero bytes, that is a sign the connection has been closed
                        int bytesSent = id.Peer.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || bytesSent == 0)
                        {
                            reason = "Sent zero";
                            Logger.Log(id, "Couldn't send message");
                            cleanup = true;
                            return;
                        }

                        // Log the data sent in both the peers and torrentmangers connection monitors
                        TransferType type = (id.Peer.Connection.CurrentlySendingMessage is PieceMessage) ? TransferType.Data : TransferType.Protocol;
                        id.Peer.Connection.SentBytes(bytesSent, type);
                        id.TorrentManager.Monitor.BytesSent(bytesSent, type);

                        // If we havn't sent everything, send the rest of the data
                        if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)
                        {
                            id.TorrentManager.Peers.Enqueue(id, PeerType.UploadQueue);
                            id.TorrentManager.ResumePeers();
                            return;
                        }
                        else
                        {
                            // Invoke the callback which we were told to invoke after we sent this message
                            id.Peer.Connection.MessageSentCallback(id);
                        }
                    }
            }
            catch (SocketException)
            {
                reason = "Exception EndSending";
                Logger.Log("Socket exception sending message");
                cleanup = true;
            }
            catch (ArgumentException ex)
            {
                reason = "FECKIN ARGUMENT EXCEPTIONS ENDSENDING!";
                cleanup = true;
                Logger.Log(id, ex.ToString());
            }
            finally
            {
                if (cleanup)
                    CleanupSocket(id, reason);
            }
        }


        private void onPeerHandshakeSent(PeerId id)
        {
            lock (id.TorrentManager.resumeLock)
            lock (id)
            {
                Logger.Log(id, "Sent Handshake");
                Logger.Log(id, "Recieving handshake");

                // Receive the handshake
                // FIXME: Will fail if protocol version changes. FIX THIS
                ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                ReceiveMessage(id, 68, this.handshakeReceievedCallback);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer handshake
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(PeerId id)
        {
            string reason = null;
            bool cleanUp = false;
            IPeerMessageInternal msg;

            try
            {
                lock (id.TorrentManager.resumeLock)
                lock (id)
                {
                    // If the connection is closed, just return
                    if (id.Peer.Connection == null)
                        return;

                    // Decode the handshake and handle it
                    msg = new HandshakeMessage();
                    msg.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
                    msg.Handle(id);

                    Logger.Log(id, "Handshake recieved");
                    HandshakeMessage handshake = msg as HandshakeMessage;

                    if (id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer)
                    {
                        if (id.TorrentManager.Bitfield.AllFalse)
                            msg = new HaveNoneMessage();

                        else if (id.TorrentManager.Bitfield.AllTrue)
                            msg = new HaveAllMessage();

                        else
                            msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                    }
                    else
                    {
                        msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                    }

                    ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
                    SendMessage(id, msg, this.bitfieldSentCallback);
                }
            }
            catch (TorrentException ex)
            {
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
            lock (id.TorrentManager.resumeLock)
            lock (id)
            {
                if (id.Peer.Connection == null)
                    return;

                // Free the old buffer and get a new one to recieve the length of the next message
                ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);

                // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
                // even if they are choked
                if (ClientEngine.SupportsFastPeer && id.Peer.Connection.SupportsFastPeer)
                    for (int i = 0; i < id.Peer.Connection.AmAllowedFastPieces.Count; i++)
                        id.Peer.Connection.EnQueue(new AllowedFastMessage(id.Peer.Connection.AmAllowedFastPieces[i]));

                // Allow the auto processing of the send queue to commence
                id.Peer.Connection.ProcessingQueue = false;

                ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message length
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageLengthReceived(PeerId id)
        {
            lock (id.TorrentManager.resumeLock)
            lock (id)
            {
                // If the connection is null, we just return
                if (id.Peer.Connection == null)
                    return;

                Logger.Log(id, "Recieved message length");

                // Decode the message length from the buffer. It is a big endian integer, so make sure
                // it is converted to host endianness.
                int messageBodyLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(id.Peer.Connection.recieveBuffer, 0));

                // Free the existing receive buffer and then get a new one which can
                // contain the amount of bytes we need to receive.
                ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);

                // If bytes to receive is zero, it means we received a keep alive message
                // so we just start receiving a new message length again
                if (messageBodyLength == 0)
                {
                    id.Peer.Connection.LastMessageReceived = DateTime.Now;
                    ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                }

                // Otherwise queue the peer in the Receive buffer and try to resume downloading off him
                else
                {
                    Logger.Log(id, "Recieving message");
                    ReceiveMessage(id, messageBodyLength, this.messageReceivedCallback);
                }
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
                lock (id.TorrentManager.resumeLock)
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    // Attempt to decode the message from the buffer.
                    //message = PeerwireEncoder.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, id.TorrentManager);
                    //message.Handle(id);
                    this.messageHandler.EnqueueReceived(id, id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);


                    // if the peer has sent us three bad pieces, we close the connection.
                    if (id.Peer.HashFails == 3)
                    {
                        reason = "3 hashfails";
                        Logger.Log(id, "3 hashfails");
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.LastMessageReceived = DateTime.Now;

                    // Free the large buffer used to recieve the piece message and get a small buffer
                    ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);

                    ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                }
            }
            catch (TorrentException ex)
            {
                reason = ex.Message;
                Logger.Log(id, "Invalid message recieved: " + ex.Message);
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
            lock (id.TorrentManager.resumeLock)
            lock (id)
            {
                // If the peer has been cleaned up, just return.
                if (id.Peer.Connection == null)
                    return;

                // Fire the event to let the user know a message was sent
                RaisePeerMessageTransferred(new PeerMessageEventArgs((IPeerMessage)id.Peer.Connection.CurrentlySendingMessage, Direction.Outgoing, id));

                ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                Logger.Log(id, "Sent message: " + id.Peer.Connection.CurrentlySendingMessage.ToString());
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                this.ProcessQueue(id);
            }
        }


        /// <summary>
        /// Receives exactly length number of bytes from the specified peer connection and invokes the supplied callback if successful
        /// </summary>
        /// <param name="id">The peer to receive the message from</param>
        /// <param name="length">The length of the message to receive</param>
        /// <param name="callback">The callback to invoke when the message has been received</param>
        private void ReceiveMessage(PeerId id, int length, MessagingCallback callback)
        {
            try
            {
                lock (id.TorrentManager.resumeLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                            return;

                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, length);

                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = length;
                        id.Peer.Connection.MessageReceivedCallback = callback;

                        id.TorrentManager.Peers.Enqueue(id, PeerType.DownloadQueue);
                        id.TorrentManager.ResumePeers();
                    }
            }
            catch (SocketException)
            {
                CleanupSocket(id, "Couldn't ReceiveMessage");
            }
        }


        /// <summary>
        /// Sends the specified message to the specified peer and invokes the supplied callback if successful
        /// </summary>
        /// <param name="id">The peer to send the message to</param>
        /// <param name="message">The  message to send</param>
        /// <param name="callback">The callback to invoke when the message has been sent</param>
        private void SendMessage(PeerId id, IPeerMessageInternal message, MessagingCallback callback)
        {
            bool cleanup = false;

            try
            {
                lock (id.TorrentManager.resumeLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                            return;

                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, message.ByteLength);
                        id.Peer.Connection.MessageSentCallback = callback;
                        id.Peer.Connection.CurrentlySendingMessage = message;
                        if (message is PieceMessage)
                            id.Peer.Connection.IsRequestingPiecesCount--;
/*
                        int count = 0;
                        for (int i = 0; i < id.TorrentManager.Peers.ConnectedPeers.Count; i++)
                            count += id.TorrentManager.Peers.ConnectedPeers[i].Peer.Connection.AmRequestingPiecesCount;
                        if (count != id.TorrentManager.PieceManager.CurrentRequestCount())
                        {
                            Logger.Log("***************************************************");
                            Logger.Log(id, "        Sending message: " + message.ToString());
                            Logger.Log(id, "        Outstanding AmRequests: " + id.Peer.Connection.AmRequestingPiecesCount);
                            Logger.Log(id, "        Outstanding IsRequests: " + id.Peer.Connection.IsRequestingPiecesCount);
                            Logger.Log(id, "        PieceManager Requests:  " + id.TorrentManager.PieceManager.CurrentRequestCount());
                            Logger.Log("***************************************************");
                        }

                        if (message is ChokeMessage || message is RejectRequestMessage || message is PieceMessage || message is RequestMessage || true)
                        {
                            Logger.Log(id, "        Sending message: " + message.ToString());
                            Logger.Log(id, "        Outstanding AmRequests: " + id.Peer.Connection.AmRequestingPiecesCount);
                            Logger.Log(id, "        Outstanding IsRequests: " + id.Peer.Connection.IsRequestingPiecesCount);
                            Logger.Log(id, "        PieceManager Requests:  " + id.TorrentManager.PieceManager.CurrentRequestCount());
                            Logger.Log(id, "");
                        }
*/
                        id.Peer.Connection.BytesSent = 0;
                        id.Peer.Connection.BytesToSend = message.Encode(id.Peer.Connection.sendBuffer, 0);

                        id.TorrentManager.Peers.Enqueue(id, PeerType.UploadQueue);
                        id.TorrentManager.ResumePeers();
                    }
            }
            catch (SocketException)
            {
                Logger.Log("SocketException in SendMessage");
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
            bool fireCleanup = false;
            if (id == null) // Sometimes onEncryptoError will fire with a null id
                return;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Logger.Log(id, "Cleanup Reason : " + message);
                        Logger.FlushToDisk(id);

                        // We can't clean up until the pending sends and receives have all had the corresponding End*** method called
                        if (!localClose && (id.Peer.ActiveReceive || id.Peer.ActiveSend))
                            return;

                        Logger.Log(id, "*******Cleaning up*******");
                        System.Threading.Interlocked.Decrement(ref this.openConnections);
                        id.TorrentManager.PieceManager.RemoveRequests(id);
                        id.Peer.CleanedUpCount++;
                        id.Peer.ActiveReceive = false;
                        id.Peer.ActiveSend = false;

                        if (id.Peer.Connection != null)
                        {
                            fireCleanup = true;
                            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);

                            if (!id.Peer.Connection.AmChoking)
                                id.TorrentManager.UploadingTo--;

                            id.Peer.Connection.Dispose();
                            id.Peer.Connection = null;
                        }
                        else
                        {
                            Logger.Log(id, "!!!!Connection already null!!!!");
                        }

                        int found = 0;
                        if (id.TorrentManager.Peers.ConnectedPeers.Contains(id))
                            found++;
                        if (id.TorrentManager.Peers.ConnectingToPeers.Contains(id))
                            found++;
                        if (id.TorrentManager.Peers.AvailablePeers.Contains(id))
                            found++;

                        if (found > 1)
                        {
                            Logger.Log("Found: " + found.ToString());
                        }

                        id.TorrentManager.Peers.RemovePeer(id, PeerType.UploadQueue);
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.DownloadQueue);

                        if (id.TorrentManager.Peers.ConnectedPeers.Contains(id))
                            id.TorrentManager.Peers.RemovePeer(id, PeerType.Connected);

                        if (id.TorrentManager.Peers.ConnectingToPeers.Contains(id))
                            id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                        if (id.Peer.PeerId != engine.PeerId)
                            if (!id.TorrentManager.Peers.AvailablePeers.Contains(id) && id.Peer.CleanedUpCount < 5)
                                id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
                    }
                }
            }
            finally
            {
                if (fireCleanup)
                    RaisePeerDisconnected(new PeerConnectionEventArgs(id, Direction.None));

                TryConnect();
            }
        }


        private void AsyncPeerConnected(object args)
        {
            if (this.PeerConnected != null)
                this.PeerConnected(null, (PeerConnectionEventArgs)args);
        }


        private void AsyncPeerMessageTransferred(object args)
        {
            if (this.PeerMessageTransferred != null)
                this.PeerMessageTransferred(null, (PeerMessageEventArgs)args);
        }


        private void AsyncPeerDisconnected(object args)
        {
            if (this.PeerDisconnected != null)
                PeerDisconnected(null, (PeerConnectionEventArgs)args);
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
            this.messageHandler.EnqueueCleanup(id);
        }


        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="result"></param>
        internal void IncomingConnectionAccepted(IAsyncResult result)
        {
            string reason = null;
            int bytesSent;
            bool cleanUp = false;
            PeerId id = (PeerId)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Interlocked.Increment(ref this.openConnections);
                        bytesSent = id.Peer.Connection.EndSend(result, out id.ErrorCode);
                        if (bytesSent == 0)
                        {
                            Logger.Log(id, "Sent 0 for incoming connection accepted");
                            reason = "sent 0 for incoming";
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        if (bytesSent != id.Peer.Connection.BytesToSend)
                        {
                            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, this.incomingConnectionAcceptedCallback, id, out id.ErrorCode);
                            return;
                        }

                        if (id.Peer.PeerId == engine.PeerId) // The tracker gave us our own IP/Port combination
                        {
                            Logger.Log(id, "Recieved myself");
                            reason = "Received myself";
                            cleanUp = true;
                            return;
                        }

                        if (id.TorrentManager.Peers.ConnectedPeers.Contains(id) || id.TorrentManager.Peers.ConnectingToPeers.Contains(id))
                        {
                            Logger.Log(id, "Already connected to peer");
                            id.Peer.Connection.Dispose();
                            return;
                        }

                        Logger.Log(id, "Peer accepted ok");
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Available);
                        id.TorrentManager.Peers.AddPeer(id, PeerType.Connected);

                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);

                        RaisePeerConnected(new PeerConnectionEventArgs(id, Direction.Incoming));

                        if ((this.openConnections >= this.MaxOpenConnections))
                        {
                            reason = "Too many peers";
                            cleanUp = true;
                            return;
                        }
                        Logger.Log(id, "Recieving message length");
                        ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                    }
                }
            }
            catch (SocketException ex)
            {
                reason = "Exception for incoming connection";
                Logger.Log(id, "Exception when accepting peer");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, reason);
            }
        }


        public void onEncryptorReady(PeerId id)
        {
            try
            {
                ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                ReceiveMessage(id, 68, this.handshakeReceievedCallback);
            }
            catch (SocketException)
            {
                CleanupSocket(id, "Exception on encryptor");
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id, "Null Ref for encryptor");
            }
        }


        public void onEncryptorError(PeerId id)
        {
            try
            {
#warning Terrible logic actually. I need to fix this.
                id.Peer.EncryptionSupported = EncryptionMethods.NoEncryption;
                CleanupSocket(id, "Encryptor error... somewhere");
                return;


                if (id.Peer.Connection != null)
                {
                    ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                    id.Peer.Connection.Dispose();
                }

                //id.Peer.Connection = null;
                //id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                if (this.engine.Settings.MinEncryptionLevel == EncryptionType.None)
                    id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
            }
            catch (SocketException)
            {
                CleanupSocket(id, "Encryptor error");
            }
            catch (NullReferenceException)
            {
                CleanupSocket(id, "Null ref encryptor error");
            }
        }


        /// <summary>
        /// This method should be called to begin processing messages stored in the SendQueue
        /// </summary>
        /// <param name="id">The peer whose message queue you want to start processing</param>
        internal void ProcessQueue(PeerId id)
        {
            if (id.Peer.Connection.QueueLength == 0)
            {
                id.Peer.Connection.ProcessingQueue = false;
                return;
            }

            IPeerMessageInternal msg = id.Peer.Connection.DeQueue();
            if (msg is PieceMessage)
                id.Peer.Connection.PiecesSent++;

            //id.Peer.MessageHistory.AppendLine(DateTime.Now.ToLongTimeString() + " " + msg.ToString());

            id.Peer.Connection.ProcessingQueue = true;
            try
            {
                SendMessage(id, msg, this.messageSentCallback);
                Logger.Log(id, "Sending message from queue: " + msg.ToString());
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception dequeuing message");
                CleanupSocket(id, "Exception calling SendMessage");
            }
        }


        internal void RaisePeerDisconnected(PeerConnectionEventArgs peerConnectionEventArgs)
        {
            if (this.PeerDisconnected != null)
                ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncPeerDisconnected), peerConnectionEventArgs);
        }


        private void RaisePeerConnected(PeerConnectionEventArgs args)
        {
            if (this.PeerConnected != null)
                ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncPeerConnected), args);
        }


        internal void RaisePeerMessageTransferred(PeerMessageEventArgs peerMessageEventArgs)
        {
            if (this.PeerMessageTransferred != null)
                ThreadPool.QueueUserWorkItem(new WaitCallback(AsyncPeerMessageTransferred), peerMessageEventArgs);
        }


        internal void RegisterManager(TorrentManager torrentManager)
        {
            lock (this.torrents)
            {
                if (!this.messageHandler.IsActive)
                    this.messageHandler.Start();

                if (this.torrents.Contains(torrentManager))
                    throw new TorrentException("TorrentManager is already registered in the connection manager");

                this.torrents.Add(torrentManager);
            }
            TryConnect();
        }


        /// <summary>
        /// Makes a peer start downloading/uploading
        /// </summary>
        /// <param name="id">The peer to resume</param>
        /// <param name="downloading">True if you want to resume downloading, false if you want to resume uploading</param>
        internal int ResumePeer(PeerId id, bool downloading)
        {
            int bytesRemaining;
            bool cleanUp = false;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                    {
                        cleanUp = true;
                        return 0;
                    }
                    if (downloading)
                    {
                        bytesRemaining = (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived);
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, bytesRemaining, SocketFlags.None, this.onEndReceiveMessageCallback, id, out id.ErrorCode);
                    }
                    else
                    {
                        bytesRemaining = (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent);
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, bytesRemaining, SocketFlags.None, this.onEndSendMessageCallback, id, out id.ErrorCode);
                    }
                }

                return bytesRemaining;
            }
            catch (SocketException)
            {
                Logger.Log("SocketException resuming");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, "Exception resuming");
            }
            return 0;
        }


        internal void TryConnect()
        {
            int i;
            PeerId id;
            TorrentManager m = null;

            // If we have already reached our max connections globally, don't try to connect to a new peer
            if ((this.openConnections >= this.MaxOpenConnections) || this.halfOpenConnections >= this.MaxHalfOpenConnections)
                return;

            // Check each torrent manager in turn to see if they have any peers we want to connect to
            lock (this.torrents)
            {
                foreach (TorrentManager manager in this.torrents)
                {
                    // If we have reached the max peers allo5wed for this torrent, don't connect to a new peer for this torrent
                    if (manager.Peers.ConnectedPeers.Count >= manager.Settings.MaxConnections)
                        continue;

                    // If the torrent isn't active, don't connect to a peer for it
                    if (manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
                        continue;

                    // If we are not seeding, we can connect to anyone. If we are seeding, we should only connect to a peer
                    // if they are not a seeder.
                    lock (manager.listLock)
                    {
                        for (i = 0; i < manager.Peers.AvailablePeers.Count; i++)
                            if ((manager.State != TorrentState.Seeding ||
                               (manager.State == TorrentState.Seeding && !manager.Peers.AvailablePeers[i].Peer.IsSeeder))
                                && (!manager.Peers.AvailablePeers[i].Peer.ActiveReceive)
                                && (!manager.Peers.AvailablePeers[i].Peer.ActiveSend)
                                && manager.Peers.AvailablePeers[i].Peer.LastConnectionAttempt < DateTime.Now.AddMinutes(-1))
                                break;

                        // If this is true, there were no peers in the available list to connect to.
                        if (i == manager.Peers.AvailablePeers.Count)
                            continue;

                        // Remove the peer from the lists so we can start connecting to him
                        id = manager.Peers.AvailablePeers[i];
                        manager.Peers.AvailablePeers.RemoveAt(i);

                        if (id.Peer.Connection != null)
                            Logger.Log("CRITICAL ERROR: Trying to connect to an already connected peer");

                        // Save the manager we're using so we can place it to the end of the list
                        m = manager;

                        // Connect to the peer
                        this.ConnectToPeer(manager, id);
                        break;
                    }
                }

                if (m == null)
                    return;

                // Put the manager at the end of the list so we try the other ones next
                this.torrents.Remove(m);
                this.torrents.Add(m);
            }
        }


        internal void UnregisterManager(TorrentManager torrentManager)
        {
            lock (this.torrents)
            {
                if (!this.torrents.Contains(torrentManager))
                    throw new TorrentException("TorrentManager is not registered in the connection manager");

                this.torrents.Remove(torrentManager);
            }
        }

        #endregion
    }
}
