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
    internal delegate void MessagingCallback(PeerIdInternal id);

    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        #region Events

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
        private AsyncCallback endCreateConnectionCallback;
        private AsyncCallback incomingConnectionAcceptedCallback;
        private AsyncCallback onEndReceiveMessageCallback;
        private AsyncCallback onEndSendMessageCallback;

        private MonoTorrentCollection<TorrentManager> torrents;

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

            this.endCheckEncryptionCallback = EndCheckEncryption;
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
            this.torrents = new MonoTorrentCollection<TorrentManager>();
            this.messageHandler = new MessageHandler();
        }

        #endregion


        #region Async Connection Methods

        internal void ConnectToPeer(TorrentManager manager, PeerIdInternal id)
        {
            // Connect to the peer.
            lock (id)
            {
                id.Connection = new PeerConnectionBase(id.TorrentManager.Torrent.Pieces.Count);
                id.Connection.Connection = ConnectionFactory.Create(id.Peer.ConnectionUri);
                if (id.Connection.Connection == null)
                    return;

                manager.Peers.AddPeer(id.Peer, PeerType.Active);
                id.TorrentManager.ConnectingToPeers.Add(id);
                
                id.Connection.ProcessingQueue = true;
                id.Peer.LastConnectionAttempt = DateTime.Now;
                id.Connection.LastMessageSent = DateTime.Now;
                id.Connection.LastMessageReceived = DateTime.Now;
                try
                {
                    id.Connection.BeginConnect(this.endCreateConnectionCallback, id);
                    id.ConnectTime = Environment.TickCount;
                    Interlocked.Increment(ref this.halfOpenConnections);
                }
                catch (Exception)
                {
                    // If there's a socket exception at this point, just drop the peer's details silently
                    // as they must be invalid.
                    manager.Peers.RemovePeer(id.Peer, PeerType.Active);
                    id.TorrentManager.ConnectingToPeers.Remove(id);
                }
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
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;
            //Console.WriteLine ("{0}ms: {1}", Environment.TickCount - id.ConnectTime, id.Peer.ConnectionUri.Host);
            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        id.TorrentManager.ConnectingToPeers.Remove(id);
                        Interlocked.Decrement(ref this.halfOpenConnections);
                        // If the peer has been cleaned up, then don't continue processing the peer
                        if (id.Connection == null)
                        {
                            Logger.Log(id.Connection.Connection, "ConnectionManager - EndCreateConnection: null already");
                            return;
                        }

                        id.Connection.EndConnect(result);
                        Logger.Log(id.Connection.Connection, "ConnectionManager - Connection opened");

                        ProcessFreshConnection(id);
                    }
                }
            }

            catch (Exception ex)
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Logger.Log(id.Connection.Connection, "ConnectionManager - Failed to connect");
                        id.Peer.FailedConnectionAttempts++;

                        if (id.Connection != null)
                        {
                            ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                            id.Connection.Dispose();
                        }

                        id.Connection = null;
                        id.NulledAt = "1";
                        id.TorrentManager.Peers.RemovePeer(id.Peer, PeerType.Active);
                        id.TorrentManager.ConnectingToPeers.Remove(id);

                        if (id.Peer.FailedConnectionAttempts < 2)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Peers.AddPeer(id.Peer, PeerType.Available);
                        else
                            id.TorrentManager.Peers.AddPeer(id.Peer, PeerType.Busy);
                    }
                }
            }
            finally
            {
                // Try to connect to another peer
                TryConnect();
            }
        }

        internal void ProcessFreshConnection(PeerIdInternal id)
        {
            bool cleanUp = false;
            string reason = null;

            try
            {
                // Remove the peer from the "connecting" list and put them in the "connected" list
                // because we have now successfully connected to them
                id.TorrentManager.ConnectingToPeers.Remove(id);
                id.TorrentManager.ConnectedPeers.Add(id);

                id.PublicId = new PeerId();
                id.UpdatePublicStats();


                // If we have too many open connections, close the connection
                if (this.openConnections > this.MaxOpenConnections)
                {
                    Logger.Log(id.Connection.Connection, "ConnectionManager - Too many connections");
                    reason = "Too many connections";
                    cleanUp = true;
                    return;
                }

                // Increase the count of the "open" connections
                System.Threading.Interlocked.Increment(ref this.openConnections);
                EncryptorFactory.BeginCheckEncryption(id, this.endCheckEncryptionCallback, id);
            }
            catch (Exception ex)
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Logger.Log(id.Connection.Connection, "failed to connect2");
                        id.Peer.FailedConnectionAttempts++;

                        if (id.Connection != null)
                        {
                            ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                            id.Connection.Dispose();
                        }

                        id.Connection = null;
                        id.NulledAt = "2";
                        id.TorrentManager.Peers.RemovePeer(id.Peer, PeerType.Active);
                        id.TorrentManager.ConnectingToPeers.Remove(id);

                        if (id.Peer.FailedConnectionAttempts < 2)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Peers.AddPeer(id.Peer, PeerType.Available);
                        else
                            id.TorrentManager.Peers.AddPeer(id.Peer, PeerType.Busy);
                    }
                }
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
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;
            byte[] initialData;
            try
            {
                EncryptorFactory.EndCheckEncryption(result, out initialData);
                if (initialData != null && initialData.Length > 0)
                {
                    Console.WriteLine("What is this initial data?!");
                    throw new EncryptionException("unhandled initial data");
                }

                // Create a handshake message to send to the peer
                HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, engine.PeerId, VersionInfo.ProtocolStringV100);
                SendMessage(id, handshake, this.handshakeSentCallback);
            }
            catch
            {
                CleanupSocket(id, "Failed encryptor check");
            }
        }


        private void EndReceiveMessage(IAsyncResult result)
        {
            string reason = null;
            bool cleanUp = false;
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        // If the connection is null, just return
                        if (id.Connection == null)
                            return;

                        // If we receive 0 bytes, the connection has been closed, so exit
                        int bytesReceived = id.Connection.EndReceive(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success)
                        {
                            reason = "EndReceiveMessage: " + id.ErrorCode.ToString();
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Error receiving message: {0}", id.ErrorCode.ToString());
                            cleanUp = true;
                            return;
                        }
                        if (bytesReceived == 0)
                        {
                            reason = "EndReceiveMessage: Received zero bytes";
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Received zero bytes instead of message");
                            cleanUp = true;
                            return;
                        }

                        // If the first byte is '7' and we're receiving more than 256 bytes (a made up number)
                        // then this is a piece message, so we add it as "data", not protocol. 256 bytes should filter out
                        // any non piece messages that happen to have '7' as the first byte.
                        TransferType type = (id.Connection.recieveBuffer.Array[id.Connection.recieveBuffer.Offset] == PieceMessage.MessageId && id.Connection.BytesToRecieve > 256) ? TransferType.Data : TransferType.Protocol;
                        id.Connection.ReceivedBytes(bytesReceived, type);
                        id.TorrentManager.Monitor.BytesReceived(bytesReceived, type);

                        // If we don't have the entire message, recieve the rest
                        if (id.Connection.BytesReceived < id.Connection.BytesToRecieve)
                        {
                            id.TorrentManager.downloadQueue.Add(id);
                            id.TorrentManager.ResumePeers();
                            return;
                        }
                        else
                        {
                            // Invoke the callback we were told to invoke once the message had been received fully
                            id.Connection.MessageReceivedCallback(id);
                        }
                    }
            }

            catch (Exception ex)
            {
                Logger.Log(id.Connection.Connection, "Exception recieving message" + ex.ToString());
                reason = "Socket Exception receiving";
                cleanUp = true;
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
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        // If the peer has disconnected, don't continue
                        if (id.Connection == null)
                            return;

                        // If we have sent zero bytes, that is a sign the connection has been closed
                        int bytesSent = id.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success)
                        {
                            reason = "Sending error: " + id.ErrorCode.ToString();
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Error sending message: {0}", id.ErrorCode.ToString());
                            cleanup = true;
                            return;
                        }
                        if (bytesSent == 0)
                        {
                            reason = "Sending error: Sent zero bytes";
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Sent zero bytes when sending a message");
                            cleanup = true;
                            return;
                        }

                        // Log the data sent in both the peers and torrentmangers connection monitors
                        TransferType type = (id.Connection.CurrentlySendingMessage is PieceMessage) ? TransferType.Data : TransferType.Protocol;
                        id.Connection.SentBytes(bytesSent, type);
                        id.TorrentManager.Monitor.BytesSent(bytesSent, type);

                        // If we havn't sent everything, send the rest of the data
                        if (id.Connection.BytesSent != id.Connection.BytesToSend)
                        {
                            id.TorrentManager.uploadQueue.Add(id);
                            id.TorrentManager.ResumePeers();
                            return;
                        }
                        else
                        {
                            // Invoke the callback which we were told to invoke after we sent this message
                            id.Connection.MessageSentCallback(id);
                        }
                    }
            }
            catch (Exception)
            {
                reason = "Exception EndSending";
                Logger.Log(id.Connection.Connection, "ConnectionManager - Socket exception sending message");
                cleanup = true;
            }
            finally
            {
                if (cleanup)
                    CleanupSocket(id, reason);
            }
        }


        private void onPeerHandshakeSent(PeerIdInternal id)
        {
            id.UpdatePublicStats();
            id.TorrentManager.RaisePeerConnected(new PeerConnectionEventArgs(id.TorrentManager, id, Direction.Outgoing));

            lock (id.TorrentManager.listLock)
            lock (id)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - Sent Handshake");

                // Receive the handshake
                // FIXME: Will fail if protocol version changes. FIX THIS
                //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                ReceiveMessage(id, 68, this.handshakeReceievedCallback);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer handshake
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(PeerIdInternal id)
        {
            string reason = null;
            bool cleanUp = false;
            PeerMessage msg;

            try
            {
                lock (id.TorrentManager.listLock)
                lock (id)
                {
                    // If the connection is closed, just return
                    if (id.Connection == null)
                        return;

                    // Decode the handshake and handle it
                    msg = new HandshakeMessage();
                    msg.Decode(id.Connection.recieveBuffer, 0, id.Connection.BytesToRecieve);
                    msg.Handle(id);

                    Logger.Log(id.Connection.Connection, "ConnectionManager - Handshake recieved");
                    if (id.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer)
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
                        btfld.SetAll (false);
                        msg = new BitfieldMessage(btfld);
                    }
                    else
                    {
                        msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                    }

                    if (id.Connection.SupportsLTMessages)
                    {
                        MessageBundle bundle = new MessageBundle();
                        bundle.Messages.Add(new ExtendedHandshakeMessage());
                        bundle.Messages.Add(msg);
                        msg = bundle;
                    }

                    //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);
                    SendMessage(id, msg, this.bitfieldSentCallback);
                }
            }
            catch (TorrentException)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - Couldn't decode the message");
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
        private void onPeerBitfieldSent(PeerIdInternal id)
        {
            lock (id.TorrentManager.listLock)
            lock (id)
            {
                if (id.Connection == null)
                    return;

                // Free the old buffer and get a new one to recieve the length of the next message
                //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);

                // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
                // even if they are choked
                if (ClientEngine.SupportsFastPeer && id.Connection.SupportsFastPeer)
                    for (int i = 0; i < id.Connection.AmAllowedFastPieces.Count; i++)
                        id.Connection.Enqueue(new AllowedFastMessage(id.Connection.AmAllowedFastPieces[i]));

                // Allow the auto processing of the send queue to commence
                id.Connection.ProcessingQueue = false;

                ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message length
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageLengthReceived(PeerIdInternal id)
        {
            lock (id.TorrentManager.listLock)
            lock (id)
            {
                // If the connection is null, we just return
                if (id.Connection == null)
                    return;

                Logger.Log(id.Connection.Connection, "ConnectionManager - Recieved message length");

                // Decode the message length from the buffer. It is a big endian integer, so make sure
                // it is converted to host endianness.
                int messageBodyLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(id.Connection.recieveBuffer.Array, id.Connection.recieveBuffer.Offset));

                // Free the existing receive buffer and then get a new one which can
                // contain the amount of bytes we need to receive.
                //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);

                // If bytes to receive is zero, it means we received a keep alive message
                // so we just start receiving a new message length again
                if (messageBodyLength == 0)
                {
                    Logger.Log(id.Connection.Connection, "ConnectionManager - Received keepalive");
                    id.Connection.LastMessageReceived = DateTime.Now;
                    ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                }

                // Otherwise queue the peer in the Receive buffer and try to resume downloading off him
                else
                {
                    Logger.Log(id.Connection.Connection, "Recieving message: {0} bytes", messageBodyLength.ToString());
                    ReceiveMessage(id, messageBodyLength, this.messageReceivedCallback);
                }
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageReceived(PeerIdInternal id)
        {
            string reason = null;
            bool cleanUp = false;

            try
            {
                lock (id.TorrentManager.listLock)
                lock (id)
                {
                    if (id.Connection == null)
                        return;

                    this.messageHandler.EnqueueReceived(id, id.Connection.recieveBuffer, 0, id.Connection.BytesToRecieve);

                    //FIXME: I thought i was using 5 (i changed the check below from 3 to 5)...
                    // if the peer has sent us three bad pieces, we close the connection.
                    if (id.Peer.TotalHashFails == 5)
                    {
                        reason = "3 hashfails";
                        Logger.Log(id.Connection.Connection, "ConnectionManager - 5 hashfails");
                        cleanUp = true;
                        return;
                    }

                    id.Connection.LastMessageReceived = DateTime.Now;

                    // Free the large buffer used to recieve the piece message and get a small buffer
                    //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);

                    ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                }
            }
            catch (TorrentException ex)
            {
                reason = ex.Message;
                Logger.Log(id.Connection.Connection, "Invalid message recieved: {0}", ex.Message);
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
        private void onPeerMessageSent(PeerIdInternal id)
        {
            lock (id.TorrentManager.listLock)
            lock (id)
            {
                // If the peer has been cleaned up, just return.
                if (id.Connection == null)
                    return;

                // Fire the event to let the user know a message was sent
                RaisePeerMessageTransferred(new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)id.Connection.CurrentlySendingMessage, Direction.Outgoing, id));

                //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                Logger.Log(id.Connection.Connection, "ConnectionManager - Sent message: " + id.Connection.CurrentlySendingMessage.ToString());
                id.Connection.LastMessageSent = DateTime.Now;
                this.ProcessQueue(id);
            }
        }


        /// <summary>
        /// Receives exactly length number of bytes from the specified peer connection and invokes the supplied callback if successful
        /// </summary>
        /// <param name="id">The peer to receive the message from</param>
        /// <param name="length">The length of the message to receive</param>
        /// <param name="callback">The callback to invoke when the message has been received</param>
        private void ReceiveMessage(PeerIdInternal id, int length, MessagingCallback callback)
        {
            ArraySegment<byte> newBuffer = BufferManager.EmptyBuffer;
            bool cleanUp = false;
            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Connection == null)
                            return;

                        if (length > RequestMessage.MaxSize)
                        {
                            Logger.Log(id.Connection.Connection, "Tried to send too much data: {0} bytes", length.ToString());
                            cleanUp = true;
                            return;
                        }

                        int alreadyReceived = (id.Connection.BytesReceived) - id.Connection.BytesToRecieve;
                        if(callback == messageLengthReceivedCallback)
                            ClientEngine.BufferManager.GetBuffer(ref newBuffer, Math.Max(alreadyReceived, length));
                        else
                            ClientEngine.BufferManager.GetBuffer(ref newBuffer, Math.Max(alreadyReceived, length + 4));

                        // Prepend the length
                        Message.Write(newBuffer.Array, newBuffer.Offset, length);
                        
                        // Copy the extra data from the old buffer into the new buffer.
                        ArraySegment<byte> oldBuffer = id.Connection.recieveBuffer;

                        if (callback == messageLengthReceivedCallback)
                            Message.Write(newBuffer.Array, newBuffer.Offset, oldBuffer.Array, oldBuffer.Offset + id.Connection.BytesToRecieve, alreadyReceived);
                        else
                            Message.Write(newBuffer.Array, newBuffer.Offset + 4, oldBuffer.Array, oldBuffer.Offset + id.Connection.BytesToRecieve, alreadyReceived);

                        // Free the old buffer and set the new buffer
                        ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);
                        id.Connection.recieveBuffer = newBuffer;

                        if (callback == messageLengthReceivedCallback)
                        {
                            id.Connection.BytesReceived = alreadyReceived;
                            id.Connection.BytesToRecieve = length;
                        }
                        else
                        {
                            id.Connection.BytesReceived = alreadyReceived + 4;
                            id.Connection.BytesToRecieve = length + 4;
                        }

                        id.Connection.MessageReceivedCallback = callback;

                        if (alreadyReceived < length)
                        {
                            id.TorrentManager.downloadQueue.Add(id);
                            id.TorrentManager.ResumePeers();
                        }
                        else
                        {
                            id.Connection.MessageReceivedCallback(id);
                        }
                    }
            }
            catch (Exception)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - Socket error receiving message");
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
        private void SendMessage(PeerIdInternal id, PeerMessage message, MessagingCallback callback)
        {
            bool cleanup = false;

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Connection == null)
                            return;
                        ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                        ClientEngine.BufferManager.GetBuffer(ref id.Connection.sendBuffer, message.ByteLength);
                        id.Connection.MessageSentCallback = callback;
                        id.Connection.CurrentlySendingMessage = message;
                        if (message is PieceMessage)
                            id.Connection.IsRequestingPiecesCount--;

                        id.Connection.BytesSent = 0;
                        id.Connection.BytesToSend = message.Encode(id.Connection.sendBuffer, 0);

                        id.TorrentManager.uploadQueue.Add(id);
                        id.TorrentManager.ResumePeers();
                    }
            }
            catch (Exception)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - Socket error sending message");
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

        internal void AsyncCleanupSocket(PeerIdInternal id, bool localClose, string message)
        {
            if (id == null) // Sometimes onEncryptoError will fire with a null id
                return;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        // It's possible the peer could be in an async send *and* receive and so end up
                        // in this block twice. This check makes sure we don't try to double dispose.
                        if (id.Connection == null)
                            return;

                        bool canResuse = id.Connection.Connection.CanReconnect;
                        Logger.Log(id.Connection.Connection, "Cleanup Reason : " + message);
  
                        Logger.Log(id.Connection.Connection, "*******Cleaning up*******");
                        System.Threading.Interlocked.Decrement(ref this.openConnections);
                        id.TorrentManager.PieceManager.RemoveRequests(id);
                        id.Peer.CleanedUpCount++;
                        id.Peer.ActiveReceive = false;
                        id.Peer.ActiveSend = false;
                        if (id.PublicId != null)
                            id.PublicId.IsValid = false;

                        ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
                        ClientEngine.BufferManager.FreeBuffer(ref id.Connection.recieveBuffer);

                        if (!id.Connection.AmChoking)
                            id.TorrentManager.UploadingTo--;

                        id.Connection.Dispose();
                        id.Connection = null;
                        id.NulledAt = "3";

                        id.TorrentManager.uploadQueue.RemoveAll(delegate(PeerIdInternal other) { return id == other; });
                        id.TorrentManager.downloadQueue.RemoveAll(delegate(PeerIdInternal other) { return id == other; });
                        id.TorrentManager.ConnectingToPeers.RemoveAll(delegate(PeerIdInternal other) { return id == other; });
                        id.TorrentManager.ConnectedPeers.RemoveAll(delegate(PeerIdInternal other) { return id == other; });

                        if (id.TorrentManager.Peers.ActivePeers.Contains(id.Peer))
                            id.TorrentManager.Peers.RemovePeer(id.Peer, PeerType.Active);

                        // If we get our own details, this check makes sure we don't try connecting to ourselves again
                        if (canResuse && id.Peer.PeerId != engine.PeerId)
                        {
                            if (!id.TorrentManager.Peers.AvailablePeers.Contains(id.Peer) && id.Peer.CleanedUpCount < 5)
                                id.TorrentManager.Peers.AvailablePeers.Insert(0, id.Peer);
                        }
                    }
                }
            }

            finally
            {
                id.TorrentManager.RaisePeerDisconnected(new PeerConnectionEventArgs(id.TorrentManager, id, Direction.None));
                TryConnect();
            }
        }



        /// <summary>
        /// This method is called when a connection needs to be closed and the resources for it released.
        /// </summary>
        /// <param name="id">The peer whose connection needs to be closed</param>
        internal void CleanupSocket(PeerIdInternal id, string message)
        {
            CleanupSocket(id, true, message);
        }


        internal void CleanupSocket(PeerIdInternal id, bool localClose, string message)
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
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Interlocked.Increment(ref this.openConnections);
                        bytesSent = id.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success)
                        {
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Sent 0 for incoming connection accepted");
                            reason = "IncomingConnectionAccepted: " + id.ErrorCode.ToString();
                            cleanUp = true;
                            return;
                        }

                        id.Connection.BytesSent += bytesSent;
                        if (bytesSent != id.Connection.BytesToSend)
                        {
                            id.Connection.BeginSend(id.Connection.sendBuffer, id.Connection.BytesSent, id.Connection.BytesToSend - id.Connection.BytesSent, SocketFlags.None, this.incomingConnectionAcceptedCallback, id, out id.ErrorCode);
                            return;
                        }

                        if (id.Peer.PeerId == engine.PeerId) // The tracker gave us our own IP/Port combination
                        {
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Recieved myself");
                            reason = "Received myself";
                            cleanUp = true;
                            return;
                        }

                        if (id.TorrentManager.Peers.ActivePeers.Contains(id.Peer))
                        {
                            Logger.Log(id.Connection.Connection, "ConnectionManager - Already connected to peer");
                            id.Connection.Dispose();
                            return;
                        }

                        Logger.Log(id.Connection.Connection, "ConnectionManager - Incoming connection fully accepted");
                        id.TorrentManager.Peers.RemovePeer(id.Peer, PeerType.Available);
                        id.TorrentManager.Peers.AddPeer(id.Peer, PeerType.Active);
                        id.TorrentManager.ConnectedPeers.Add(id);

                        //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);

                        id.PublicId = new PeerId();
                        id.UpdatePublicStats();
                        id.TorrentManager.RaisePeerConnected (new PeerConnectionEventArgs(id.TorrentManager, id, Direction.Incoming));

                        if (this.openConnections >= Math.Min(this.MaxOpenConnections, id.TorrentManager.Settings.MaxConnections))
                        {
                            reason = "Too many peers";
                            cleanUp = true;
                            return;
                        }
                        if (id.TorrentManager.IsInitialSeeding)
                        {
                            int pieceIndex = id.TorrentManager.InitialSeed.GetNextPieceForPeer (id);
                            if (pieceIndex != -1) {
                                // If the peer has the piece already, we need to recalculate his "interesting" status.
                                bool hasPiece = id.Connection.BitField[pieceIndex];
                            
                                // Check to see if have supression is enabled and send the have message accordingly
                                if (!hasPiece || (hasPiece && !this.engine.Settings.HaveSupressionEnabled))
                                    id.Connection.Enqueue(new HaveMessage(pieceIndex));
                            }
                        }
                        Logger.Log(id.Connection.Connection, "ConnectionManager - Recieving message length");
                        ClientEngine.BufferManager.GetBuffer(ref id.Connection.recieveBuffer, 68);
                        ReceiveMessage(id, 4, this.messageLengthReceivedCallback);
                    }
                }
            }
            catch (Exception)
            {
                reason = "Exception for incoming connection";
                Logger.Log(id.Connection.Connection, "Socket exception when accepting peer");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, reason);
            }
        }


        internal void onEncryptorReady(PeerIdInternal id)
        {
            try
            {
                //ClientEngine.BufferManager.FreeBuffer(ref id.Connection.sendBuffer);
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
        internal void ProcessQueue(PeerIdInternal id)
        {
            if (id.Connection.QueueLength == 0)
            {
                id.Connection.ProcessingQueue = false;
                return;
            }

            PeerMessage msg = id.Connection.Dequeue();
            if (msg is PieceMessage)
                id.Connection.PiecesSent++;

            id.Connection.ProcessingQueue = true;
            try
            {
                SendMessage(id, msg, this.messageSentCallback);
                Logger.Log(id.Connection.Connection, "ConnectionManager - Sending message from queue: {0}", msg.ToString());
            }
            catch (Exception)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - Socket exception dequeuing message");
                CleanupSocket(id, "Exception calling SendMessage");
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
            lock (this.torrents)
            {
                if (this.torrents.Contains(torrentManager))
                    throw new TorrentException("TorrentManager is already registered in the connection manager");

                if (!this.messageHandler.IsActive)
                    this.messageHandler.Start();

                this.torrents.Add(torrentManager);
            }
            TryConnect();
        }


        /// <summary>
        /// Makes a peer start downloading/uploading
        /// </summary>
        /// <param name="id">The peer to resume</param>
        /// <param name="downloading">True if you want to resume downloading, false if you want to resume uploading</param>
        internal int ResumePeer(PeerIdInternal id, bool downloading)
        {
            // We attempt to send/receive 'bytecount' number of bytes
            int byteCount;
            bool cleanUp = false;

            try
            {
                lock (id)
                {
                    if (id.Connection == null)
                    {
                        cleanUp = true;
                        return 0;
                    }
                    if (downloading)
                    {
                        byteCount = (id.Connection.BytesToRecieve - id.Connection.BytesReceived) > ChunkLength ? ChunkLength : id.Connection.BytesToRecieve - id.Connection.BytesReceived;
                        id.Connection.BeginReceive(id.Connection.recieveBuffer, id.Connection.BytesReceived, byteCount, SocketFlags.None, this.onEndReceiveMessageCallback, id, out id.ErrorCode);
                    }
                    else
                    {
                        byteCount = (id.Connection.BytesToSend - id.Connection.BytesSent) > ChunkLength ? ChunkLength : (id.Connection.BytesToSend - id.Connection.BytesSent);
                        id.Connection.BeginSend(id.Connection.sendBuffer, id.Connection.BytesSent, byteCount, SocketFlags.None, this.onEndSendMessageCallback, id, out id.ErrorCode);
                    }
                }

                return byteCount;
            }
            catch (Exception)
            {
                Logger.Log(id.Connection.Connection, "ConnectionManager - SocketException resuming");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id, "Exception resuming");
            }
            return 0;
        }

        private void ClosePendingConnections ()
        {
            lock (this.torrents)
            {
                foreach (TorrentManager manager in this.torrents)
                {
                    lock (manager.listLock)
                    {
                        foreach (PeerIdInternal peer in manager.ConnectingToPeers)
                        {
                            // If we haven't connected after 10 seconds, abort
                            if ((Environment.TickCount - peer.ConnectTime) > 10000)
                                peer.Connection.Connection.Dispose ();
                        }
                    }
                }
            }
        }

        internal void TryConnect()
        {
            try
            {
                ClosePendingConnections ();
                int i;
                PeerIdInternal id;
                TorrentManager m = null;

                // If we have already reached our max connections globally, don't try to connect to a new peer
                if ((this.openConnections >= this.MaxOpenConnections) || this.halfOpenConnections >= this.MaxHalfOpenConnections)
                    return;

                // Check each torrent manager in turn to see if they have any peers we want to connect to
                lock (this.torrents)
                {
                    foreach (TorrentManager manager in this.torrents)
                    {
                        // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
                        if (manager.ConnectedPeers.Count >= manager.Settings.MaxConnections)
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
                                   (manager.State == TorrentState.Seeding && !manager.Peers.AvailablePeers[i].IsSeeder))
                                    && (!manager.Peers.AvailablePeers[i].ActiveReceive)
                                    && (!manager.Peers.AvailablePeers[i].ActiveSend))
                                    break;

                            // If this is true, there were no peers in the available list to connect to.
                            if (i == manager.Peers.AvailablePeers.Count)
                                continue;

                            // Remove the peer from the lists so we can start connecting to him
                            id = new PeerIdInternal(manager.Peers.AvailablePeers[i], manager);
                            manager.Peers.AvailablePeers.RemoveAt(i);

                            // Save the manager we're using so we can place it to the end of the list
                            m = manager;

                            // Connect to the peer
                            ConnectToPeer(manager, id);
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
            catch (Exception ex)
            {
                engine.RaiseCriticalException(new CriticalExceptionEventArgs(ex, engine));
            }
        }


        internal void UnregisterManager(TorrentManager torrentManager)
        {
            lock (this.torrents)
            {
                if (!this.torrents.Contains(torrentManager))
                    throw new TorrentException("TorrentManager is not registered in the connection manager");

                this.torrents.Remove(torrentManager);

                if (this.messageHandler.IsActive && this.torrents.Count == 0)
                    this.messageHandler.Stop();
            }
        }

        #endregion

        internal bool IsRegistered(TorrentManager torrentManager)
        {
            return this.torrents.Contains(torrentManager);
        }
    }
}
