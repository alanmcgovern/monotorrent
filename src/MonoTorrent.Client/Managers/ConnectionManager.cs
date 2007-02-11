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

namespace MonoTorrent.Client
{
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
        public const int ChunkLength = 2048;   // Download in 2kB chunks to allow for better rate limiting

        private EngineSettings settings;


        // Create the callbacks and reuse them. Reduces ongoing allocations by a fair few megs
        private AsyncCallback bitfieldSentCallback;
        private AsyncCallback endCreateConnectionCallback;
        private AsyncCallback handshakeReceievedCallback;
        private AsyncCallback handshakeSentCallback;
        private AsyncCallback incomingConnectionAcceptedCallback;
        private AsyncCallback messageLengthReceivedCallback;
        private AsyncCallback messageReceivedCallback;
        private AsyncCallback messageSentCallback;


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
            get { return this.settings.GlobalMaxHalfOpenConnections; }
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
            get { return this.settings.GlobalMaxConnections; }
        }
        #endregion


        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        public ConnectionManager(EngineSettings settings)
        {
            this.settings = settings;

            this.bitfieldSentCallback = new AsyncCallback(this.OnPeerBitfieldSent);
            this.endCreateConnectionCallback = new AsyncCallback(this.EndCreateConnection);
            this.handshakeSentCallback = new AsyncCallback(this.onPeerHandshakeSent);
            this.handshakeReceievedCallback = new AsyncCallback(this.onPeerHandshakeReceived);
            this.incomingConnectionAcceptedCallback = new AsyncCallback(IncomingConnectionAccepted);
            this.messageLengthReceivedCallback = new AsyncCallback(this.onPeerMessageLengthReceived);
            this.messageReceivedCallback = new AsyncCallback(this.onPeerMessageReceived);
            this.messageSentCallback = new AsyncCallback(this.onPeerMessageSent);
        }
        #endregion


        #region Async Connection Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="manager"></param>
        internal void ConnectToPeer(TorrentManager manager)
        {
            if ((this.openConnections >= this.MaxOpenConnections) || this.halfOpenConnections >= this.MaxHalfOpenConnections)
                return;

            int i;
            PeerConnectionID id = null;
            int length = manager.Peers.AvailablePeers.Count;

            for (i = 0; i < length; i++)
            {
                id = manager.Peers.AvailablePeers[0];
                manager.Peers.RemovePeer(id, PeerType.Available);

                // If the peer is a known seeder and i'm a seeder, don't bother trying to connect
                if (manager.State == TorrentState.Seeding && id.Peer.IsSeeder)
                {
                    manager.Peers.AddPeer(id, PeerType.Available);
                    id = null;
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (id == null)
                return;

            lock (id)
            {
                Logger.Log(id, "Connecting");
                manager.Peers.AddPeer(id, PeerType.Connecting);
                System.Threading.Interlocked.Increment(ref this.halfOpenConnections);
                id.Peer.Connection = new TCPConnection(id.Peer.Location, id.TorrentManager.Torrent.Pieces.Length, new NoEncryption());
                id.Peer.Connection.ProcessingQueue = true;
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                id.Peer.Connection.LastMessageReceived = DateTime.Now;
                id.Peer.Connection.BeginConnect(this.endCreateConnectionCallback, id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we try to create a remote connection
        /// </summary>
        /// <param name="result"></param>
        private void EndCreateConnection(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null");
                            return;
                        }

                        id.Peer.Connection.EndConnect(result);
                        Logger.Log(id, "Connected");
                        //id.Peer.MessageHistory.AppendLine(DateTime.Now.ToLongTimeString() + " ****Connected****");
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);
                        id.TorrentManager.Peers.AddPeer(id, PeerType.Connected);

                        if (this.PeerConnected != null)
                            this.PeerConnected(null, new PeerConnectionEventArgs(id, Direction.Outgoing));

                        if (this.openConnections > this.MaxOpenConnections)
                        {
                            Logger.Log(id, "Too many connections");
                            cleanUp = true;
                            return;
                        }

                        HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, ClientEngine.PeerId, VersionInfo.ProtocolStringV100);

                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.SmallMessageBuffer);
                        id.Peer.Connection.BytesToSend = 0;
                        id.Peer.Connection.BytesToSend += handshake.Encode(id.Peer.Connection.sendBuffer, 0);

                        System.Threading.Interlocked.Increment(ref this.openConnections);
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, this.handshakeSentCallback, id, out id.ErrorCode);
                    }
                }
            }

            catch (SocketException ex)
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        Logger.Log(id, "failed to connect " + ex.Message);
                        id.Peer.FailedConnectionAttempts++;

                        if (id.Peer.Connection != null)
                        {
                            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                            id.Peer.Connection.ProcessingQueue = false;
                            id.Peer.Connection.Dispose();
                        }
                        id.Peer.Connection = null;

                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                        if (id.Peer.FailedConnectionAttempts < 2)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
                    }
                }
            }
            //catch (ArgumentException)
            //{
            //    lock (id.TorrentManager.listLock)
            //    {
            //        lock (id)
            //        {
            //            Logger.Log(id, "Failed to connect, argument exception");
            //            ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
            //            id.Peer.FailedConnectionAttempts++;
            //            id.Peer.Connection.ProcessingQueue = false;
            //            if (id.Peer.Connection != null)
            //                id.Peer.Connection.Dispose();
            //            id.Peer.Connection = null;

            //            id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

            //            if (id.Peer.FailedConnectionAttempts < 4)   // We couldn't connect this time, so re-add to available
            //                id.TorrentManager.Peers.AddPeer(id, PeerType.Available);

            //        }
            //    }
            //}
            finally
            {
                System.Threading.Interlocked.Decrement(ref this.halfOpenConnections);
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we send our handshake message
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeSent(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                            return;

                        int bytesSent = id.Peer.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || bytesSent == 0)
                        {
                            Logger.Log(id, "Couldn't send handshake");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        id.Peer.Connection.Monitor.BytesSent(bytesSent);
                        id.TorrentManager.ProtocolBytesUploaded += bytesSent;

                        if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)     // If we havn't sent everything
                        {
                            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, handshakeSentCallback, id, out id.ErrorCode);
                            return;
                        }
                        Logger.Log(id, "Sent Handshake");
                        Logger.Log(id, "Recieving handshake");
                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);
                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = 68;       // FIXME: Will fail if protocol version changes. FIX THIS
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, handshakeReceievedCallback, id, out id.ErrorCode);
                    }
            }

            catch (SocketException ex)
            {
                Logger.Log(id, "Exception sending handshake");
                Debug.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
            //catch (ArgumentException)
            //{
            //    cleanUp = true;
            //}
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer handshake
        /// </summary>
        /// <param name="result"></param>
        private void onPeerHandshakeReceived(IAsyncResult result)
        {
            bool cleanUp = false;
            IPeerMessageInternal msg;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null");
                            return;
                        }

                        int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || bytesReceived == 0)
                        {
                            Logger.Log(id, "Recieved 0 byte handshake");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesReceived += bytesReceived;
                        id.Peer.Connection.Monitor.BytesReceived(bytesReceived);
                        id.TorrentManager.ProtocolBytesDownloaded += bytesReceived;

                        if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                        {
                            id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, this.handshakeReceievedCallback, id, out id.ErrorCode);
                            return;
                        }
                        Logger.Log(id, "Handshake recieved");
                        msg = new HandshakeMessage();
                        msg.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);
                        HandshakeMessage handshake = msg as HandshakeMessage;

                        // If we got the peer as a "compact" peer, then the peerid will be empty
                        if (string.IsNullOrEmpty(id.Peer.PeerId))
                            id.Peer.PeerId = handshake.PeerId;

                        // If the infohash doesn't match, dump the connection
                        if (!ToolBox.ByteMatch(handshake.infoHash, id.TorrentManager.Torrent.InfoHash))
                        {
                            Logger.Log(id, "Invalid infohash");
                            cleanUp = true;
                            return;
                        }

                        // If the peer id's don't match, dump the connection
                        if (id.Peer.PeerId != handshake.PeerId)
                        {
                            Logger.Log(id, "Invalid peerid");
                            cleanUp = true;
                            return;
                        }

                        handshake.Handle(id);

                        if (id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer)
                        {
                            if (id.TorrentManager.Bitfield.AllFalse())
                                msg = new HaveNoneMessage();

                            else if (id.TorrentManager.Progress == 100.0)
                                msg = new HaveAllMessage();

                            else
                                msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                        }
                        else
                        {
                            msg = new BitfieldMessage(id.TorrentManager.Bitfield);
                        }

                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.LargeMessageBuffer);
                        Logger.Log(id, "Sending bitfield: " + msg.GetType().Name);
                        id.Peer.Connection.BytesSent = 0;
                        id.Peer.Connection.BytesToSend = msg.Encode(id.Peer.Connection.sendBuffer, 0);
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, this.bitfieldSentCallback, id, out id.ErrorCode);
                    }
            }

            catch (SocketException ex)
            {
                Logger.Log(id, "Exception recieving handshake");
                Debug.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
            //catch (ArgumentException)
            //{
            //    cleanUp = true;
            //}
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void OnPeerBitfieldSent(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null for sending bitfield");
                            return;
                        }

                        int bytesSent = id.Peer.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || (bytesSent == 0))
                        {
                            Logger.Log(id, "Sent 0 bytes for bitfield");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        id.Peer.Connection.Monitor.BytesSent(bytesSent);
                        id.TorrentManager.ProtocolBytesUploaded += bytesSent;

                        if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)
                        {
                            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, this.bitfieldSentCallback, id, out id.ErrorCode);
                            return;
                        }
                        
                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);
                        id.Peer.Connection.ProcessingQueue = false;

                        // Now we will enqueue a FastPiece message for each piece we will allow the peer to download
                        if(ClientEngine.SupportsFastPeer && id.Peer.Connection.SupportsFastPeer)
                            for (int i = 0; i < id.Peer.Connection.AmAllowedFastPieces.Count; i++)
                                id.Peer.Connection.EnQueue(new AllowedFastMessage(id.Peer.Connection.AmAllowedFastPieces[i]));

                        Logger.Log(id, "Queuing");
                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = 4;
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.messageLengthReceivedCallback, id, out id.ErrorCode);
                    }
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception sending bitfield");
                cleanUp = true;
            }
            //catch (ArgumentException)
            //{
            //    cleanUp = true;
            //}
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message length
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageLengthReceived(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null for message length");
                            return;
                        }
                        int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || bytesReceived == 0)
                        {
                            Logger.Log(id, "Recieved 0 for message length");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesReceived += bytesReceived;
                        id.Peer.Connection.Monitor.BytesReceived(bytesReceived);
                        id.TorrentManager.ProtocolBytesDownloaded += bytesReceived;

                        if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                        {
                            id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, this.messageLengthReceivedCallback, id, out id.ErrorCode);
                            return;
                        }
                        Logger.Log(id, "Recieved message length");
                        int networkOrderLength = BitConverter.ToInt32(id.Peer.Connection.recieveBuffer, 0);
                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = System.Net.IPAddress.NetworkToHostOrder(networkOrderLength);

                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);

                        if (id.Peer.Connection.BytesToRecieve > BufferManager.SmallMessageBufferSize)
                            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.LargeMessageBuffer);
                        else
                            ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);

                        if (id.Peer.Connection.BytesToRecieve == 0)     // We Received a KeepAlive
                        {
                            id.Peer.Connection.LastMessageReceived = DateTime.Now;
                            id.Peer.Connection.BytesToRecieve = 4;
                            id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.messageLengthReceivedCallback, id, out id.ErrorCode);
                        }
                        else
                        {
                            Logger.Log(id, "Recieving message");
                            if (id.TorrentManager.Settings.MaxDownloadSpeed == 0)   // No rate limiting needed
                            {
                                id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.messageReceivedCallback, id, out id.ErrorCode);
                                return;
                            }
                            else    // Apply rate limiting
                            {
                                id.TorrentManager.Peers.Enqueue(id, PeerType.DownloadQueue);
                                id.TorrentManager.ResumePeers();
                            }
                        }
                    }
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception recieving message length");
                Debug.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
            //catch (ArgumentException)
            //{
            //    cleanUp = true;
            //}
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when we recieve a peer message
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageReceived(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            
            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null recieving message");
                            return;
                        }

                        int bytesReceived = id.Peer.Connection.EndReceive(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || (bytesReceived == 0))
                        {
                            Logger.Log(id, "Recieved 0 for message");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesReceived += bytesReceived;
                        id.TorrentManager.DataBytesDownloaded += bytesReceived;
                        id.Peer.Connection.Monitor.BytesReceived(bytesReceived);

                        if (id.Peer.Connection.BytesReceived != id.Peer.Connection.BytesToRecieve)
                        {
                            if (id.TorrentManager.Settings.MaxDownloadSpeed == 0)
                            {
                                id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesReceived, SocketFlags.None, this.messageReceivedCallback, id, out id.ErrorCode);
                            }
                            else
                            {
                                id.TorrentManager.Peers.Enqueue(id, PeerType.DownloadQueue);
                                id.TorrentManager.ResumePeers();
                            }
                            return;
                        }

                        IPeerMessageInternal message;
                        try
                        {
                            message = PeerwireEncoder.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, id.TorrentManager);
                        }
                        catch (ProtocolException ex)
                        {
                            Logger.Log(id, "Invalid message recieved");
                            Trace.WriteLine(ex.Message);
                            cleanUp = true;
                            return;
                        }

                        if (this.PeerMessageTransferred != null)
                            this.PeerMessageTransferred(id, new PeerMessageEventArgs((IPeerMessage)message, Direction.Incoming));
                        try
                        {
                            message.Handle(id);
                        }
                        catch (TorrentException ex)
                        {
                            Logger.Log(id, "Couldn't handle message");
                            cleanUp = true;
                            return;
                        }
                        Logger.Log(id, "Recieved message: " +message.GetType().Name);
                        if (!(message is PieceMessage))
                        {   // The '-4' is because of the messagelength int which has already been counted in a different method
                            id.TorrentManager.DataBytesDownloaded -= (message.ByteLength - 4);
                            id.TorrentManager.ProtocolBytesDownloaded += (message.ByteLength - 4);
                        }

                        id.TorrentManager.Monitor.BytesReceived(message.ByteLength);
                        if (id.Peer.HashFails == 3)
                        {
                            Logger.Log(id, "3 hashfails");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.LastMessageReceived = DateTime.Now;

                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.recieveBuffer);
                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);

                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = 4;
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.messageLengthReceivedCallback, id, out id.ErrorCode);
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception recieving message");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called as part of the AsyncCallbacks when a peer message is sent
        /// </summary>
        /// <param name="result"></param>
        private void onPeerMessageSent(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;
            

            try
            {
                lock (id.TorrentManager.listLock)
                    lock (id)
                    {
                        if (id.Peer.Connection == null)
                        {
                            Logger.Log(id, "Connection null sending message");
                            return;
                        }

                        int bytesSent = id.Peer.Connection.EndSend(result, out id.ErrorCode);
                        if (id.ErrorCode != SocketError.Success || (bytesSent == 0))
                        {
                            Logger.Log(id, "Sent 0 for message");
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        if (id.Peer.Connection.CurrentlySendingMessage is PieceMessage)
                            id.TorrentManager.DataBytesUploaded += bytesSent;
                        else
                            id.TorrentManager.ProtocolBytesUploaded += bytesSent;

                        id.Peer.Connection.Monitor.BytesSent(bytesSent);

                        if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)
                        {
                            if (id.TorrentManager.Settings.MaxUploadSpeed == 0)
                            {
                                id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, this.messageSentCallback, id, out id.ErrorCode);
                            }
                            else
                            {
                                id.TorrentManager.Peers.Enqueue(id, PeerType.UploadQueue);
                                id.TorrentManager.ResumePeers();
                            }
                            return;
                        }

                        if (this.PeerMessageTransferred != null)
                            this.PeerMessageTransferred(id, new PeerMessageEventArgs((IPeerMessage)id.Peer.Connection.CurrentlySendingMessage, Direction.Outgoing));

                        ClientEngine.BufferManager.FreeBuffer(ref id.Peer.Connection.sendBuffer);
                        Logger.Log(id, "Sent message: " +id.Peer.Connection.CurrentlySendingMessage.GetType().Name);
                        id.Peer.Connection.LastMessageSent = DateTime.Now;
                        this.ProcessQueue(id);
                    }
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception sending message");
                cleanUp = true;
            }
            //catch (ArgumentException)
            //{
            //    cleanUp = true;
            //}
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// This method should be called to begin processing messages stored in the SendQueue
        /// </summary>
        /// <param name="id">The peer whose message queue you want to start processing</param>
        internal void ProcessQueue(PeerConnectionID id)
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
                if (msg.ByteLength > BufferManager.SmallMessageBufferSize)
                    ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.LargeMessageBuffer);
                else
                    ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.sendBuffer, BufferType.SmallMessageBuffer);

                id.Peer.Connection.BytesSent = 0;
                id.Peer.Connection.BytesToSend = msg.Encode(id.Peer.Connection.sendBuffer, 0);
                id.Peer.Connection.CurrentlySendingMessage = msg;

                Logger.Log(id, "Sending message from queue: " + msg.ToString());
                if (id.TorrentManager.Settings.MaxUploadSpeed == 0)
                {
                    id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, this.messageSentCallback, id, out id.ErrorCode);
                }
                else
                {
                    id.TorrentManager.Peers.Enqueue(id, PeerType.UploadQueue);
                    id.TorrentManager.ResumePeers();
                }
                return;
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception dequeuing message");
                CleanupSocket(id);
            }
        }


        /// <summary>
        /// Makes a peer start downloading/uploading
        /// </summary>
        /// <param name="id">The peer to resume</param>
        /// <param name="downloading">True if you want to resume downloading, false if you want to resume uploading</param>
        internal int ResumePeer(PeerConnectionID id, bool downloading)
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
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, bytesRemaining, SocketFlags.None, this.messageReceivedCallback, id, out id.ErrorCode);
                    }
                    else
                    {
                        bytesRemaining = (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent);
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, bytesRemaining, SocketFlags.None, this.messageSentCallback, id, out id.ErrorCode);
                    }
                }

                return bytesRemaining;
            }
            catch (SocketException)
            {
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
            return 0;
        }


        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="result"></param>
        internal void IncomingConnectionAccepted(IAsyncResult result)
        {
            int bytesSent;
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

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
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        if (bytesSent != id.Peer.Connection.BytesToSend)
                        {
                            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, this.incomingConnectionAcceptedCallback, id, out id.ErrorCode);
                            return;
                        }

                        if (id.Peer.PeerId == ClientEngine.PeerId) // The tracker gave us our own IP/Port combination
                        {
                            Logger.Log(id, "Recieved myself");
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
                        ClientEngine.BufferManager.GetBuffer(ref id.Peer.Connection.recieveBuffer, BufferType.SmallMessageBuffer);

                        if (this.PeerConnected != null)
                            this.PeerConnected(null, new PeerConnectionEventArgs(id, Direction.Incoming));

                        Logger.Log(id, "Recieving message length");
                        id.Peer.Connection.BytesReceived = 0;
                        id.Peer.Connection.BytesToRecieve = 4;
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesReceived, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.messageLengthReceivedCallback, id, out id.ErrorCode);
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.Log(id, "Exception when accepting peer");
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }


        /// <summary>
        /// This method is called when a connection needs to be closed and the resources for it released.
        /// </summary>
        /// <param name="id">The peer whose connection needs to be closed</param>
        internal void CleanupSocket(PeerConnectionID id)
        {
            lock (id.TorrentManager.listLock)
            {
                lock (id)
                {
                    Logger.Log(id, "*******Cleaning up*******");
                    System.Threading.Interlocked.Decrement(ref this.openConnections);
                    id.TorrentManager.PieceManager.RemoveRequests(id);
                    id.Peer.CleanedUpCount++;
                    //id.Peer.MessageHistory.AppendLine(DateTime.Now.ToLongTimeString() + " ****Cleaning Up****");

                    if (id.Peer.Connection != null)
                    {
                        if (this.PeerDisconnected != null)
                            this.PeerDisconnected(null, new PeerConnectionEventArgs(id, Direction.None));

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
                    if(id.TorrentManager.Peers.ConnectedPeers.Contains(id))
                        found++;
                    if (id.TorrentManager.Peers.ConnectingToPeers.Contains(id))
                        found++;
                    if (id.TorrentManager.Peers.AvailablePeers.Contains(id))
                        found++;

                    if (found > 1)
                    {
                        Console.WriteLine("Found: " + found.ToString());
                    }

                    id.TorrentManager.Peers.RemovePeer(id, PeerType.UploadQueue);
                    id.TorrentManager.Peers.RemovePeer(id, PeerType.DownloadQueue);

                    if (id.TorrentManager.Peers.ConnectedPeers.Contains(id))
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connected);

                    if (id.TorrentManager.Peers.ConnectingToPeers.Contains(id))
                        id.TorrentManager.Peers.RemovePeer(id, PeerType.Connecting);

                    if (id.Peer.PeerId != ClientEngine.PeerId)
                        if (!id.TorrentManager.Peers.AvailablePeers.Contains(id) && id.Peer.CleanedUpCount < 5)
                            id.TorrentManager.Peers.AddPeer(id, PeerType.Available);
                }
            }
        }
        #endregion
    }
}
