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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        #region Events
        /// <summary>
        /// Event that's fired every time a Peer connects or disconnects
        /// </summary>
        public event EventHandler<PeerConnectionEventArgs> OnPeerConnectionChanged;


        /// <summary>
        /// Event that's fired every time a message is sent or recieved from a Peer
        /// </summary>
        public event EventHandler<PeerMessageEventArgs> OnPeerMessages;
        #endregion


        #region Member Variables
        private const int ChunkLength = 2048;

        private EngineSettings settings;

        private AsyncCallback peerEndCreateConnection;
        private AsyncCallback peerMessageLengthRecieved;
        private AsyncCallback peerMessageRecieved;
        private AsyncCallback peerMessageSent;
        private AsyncCallback peerHandshakeReceieved;
        private AsyncCallback peerHandshakeSent;
        private AsyncCallback peerBitfieldSent;


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

            this.peerMessageRecieved = new AsyncCallback(this.onPeerMessageRecieved);
            this.peerMessageLengthRecieved = new AsyncCallback(this.onPeerMessageLengthRecieved);
            this.peerMessageSent = new AsyncCallback(this.onPeerMessageSent);
            this.peerHandshakeSent = new AsyncCallback(this.onPeerHandshakeSent);
            this.peerHandshakeReceieved = new AsyncCallback(this.onPeerHandshakeRecieved);
            this.peerEndCreateConnection = new AsyncCallback(this.EndCreateConnection);
            this.peerBitfieldSent = new AsyncCallback(this.OnPeerBitfieldSent);
        }
        #endregion


        #region Async Connection Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="manager"></param>
        public void ConnectToPeer(TorrentManager manager)
        {
            if ((this.openConnections >= this.MaxOpenConnections) || this.halfOpenConnections >= this.MaxHalfOpenConnections)
                return;

            PeerConnectionID id;
            System.Threading.Interlocked.Increment(ref this.halfOpenConnections);

            id = manager.Available[0];
            manager.Available.Remove(0);
            manager.ConnectingTo.Add(id);
            lock (id)
            {
                id.Peer.Connection = new TCPConnection(id.Peer.Location, id.TorrentManager.Torrent.Pieces.Length, new NoEncryption());
                id.Peer.Connection.ProcessingQueue = true;
                id.Peer.Connection.LastMessageSent = DateTime.Now;
                id.Peer.Connection.BeginConnect(peerEndCreateConnection, id);
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
                            return;

                        id.Peer.Connection.EndConnect(result);

                        id.TorrentManager.ConnectingTo.Remove(id);
                        id.TorrentManager.ConnectedPeers.Add(id);

                        if (this.OnPeerConnectionChanged != null)
                            this.OnPeerConnectionChanged(id, new PeerConnectionEventArgs(PeerConnectionEvent.OutgoingConnectionCreated));

                        if (this.openConnections > this.MaxOpenConnections)
                        {
                            cleanUp = true;
                            return;
                        }

                        //FIXME: Can this be optimised more? How many allocations is this responsible for?
                        HandshakeMessage handshake = new HandshakeMessage(id.TorrentManager.Torrent.InfoHash, ClientEngine.PeerId, VersionInfo.ProtocolStringV100);

                        id.Peer.Connection.BytesToSend = 0;
                        id.Peer.Connection.BytesToSend += handshake.Encode(id.Peer.Connection.sendBuffer, 0);

                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, peerHandshakeSent, id);
                        System.Threading.Interlocked.Increment(ref this.openConnections);
                        System.Threading.Interlocked.Decrement(ref this.halfOpenConnections);
                    }
                }
            }

            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        id.Peer.FailedConnectionAttempts++;
                        id.Peer.Connection.ProcessingQueue = false;
                        if(id.Peer.Connection != null)
                            id.Peer.Connection.Dispose();
                        id.Peer.Connection = null;

                        id.TorrentManager.ConnectingTo.Remove(id);

                        if (id.Peer.FailedConnectionAttempts < 10)   // We couldn't connect this time, so re-add to available
                            id.TorrentManager.Available.Add(id);

                        System.Threading.Interlocked.Decrement(ref this.halfOpenConnections);
                    }
                }
            }
            finally
            {
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
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesSent = id.Peer.Connection.EndSend(result);
                    if (bytesSent == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesSent += bytesSent;

                    if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)     // If we havn't sent everything
                    {
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, peerHandshakeSent, id);
                        return;
                    }

                    id.Peer.Connection.BytesRecieved = 0;
                    id.Peer.Connection.BytesToRecieve = 68;       // FIXME: Will fail if protocol version changes. FIX THIS
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerHandshakeReceieved, id);
                }
            }

            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
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
        private void onPeerHandshakeRecieved(IAsyncResult result)
        {
            bool cleanUp = false;
            IPeerMessage msg;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesRecieved = id.Peer.Connection.EndReceive(result);
                    if (bytesRecieved == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesRecieved += bytesRecieved;
                    if (id.Peer.Connection.BytesRecieved != id.Peer.Connection.BytesToRecieve)
                    {
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesRecieved, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved, SocketFlags.None, peerHandshakeReceieved, id);
                        return;
                    }

                    msg = new HandshakeMessage();
                    msg.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve);

                    if (!ToolBox.ByteMatch(((HandshakeMessage)msg).infoHash, id.TorrentManager.Torrent.InfoHash))
                    {
                        cleanUp = true;
                        return;
                    }

                    if (((HandshakeMessage)msg).SupportsFastPeer)
                    {
                        id.Peer.Connection.SupportsFastPeer = true;
                        if (id.TorrentManager.PieceManager.MyBitField.AllFalse())
                            msg = new HaveNoneMessage();

                        else if (id.TorrentManager.State == TorrentState.Seeding || id.TorrentManager.State == TorrentState.SuperSeeding)
                            msg = new HaveAllMessage();

                        else
                            msg = new BitfieldMessage(id.TorrentManager.PieceManager.MyBitField);
                    }
                    else
                    {
                        msg = new BitfieldMessage(id.TorrentManager.PieceManager.MyBitField);
                    }

                    id.Peer.Connection.BytesSent = 0;
                    id.Peer.Connection.BytesToSend = msg.Encode(id.Peer.Connection.sendBuffer, 0);
                    id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, 0, id.Peer.Connection.BytesToSend, SocketFlags.None, this.peerBitfieldSent, id);
                }
            }

            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
        }

        private void OnPeerBitfieldSent(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesSent = id.Peer.Connection.EndSend(result);
                    if (bytesSent == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesSent += bytesSent;
                    if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)
                    {
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, peerBitfieldSent, id);
                        return;
                    }

                    id.Peer.Connection.BytesRecieved = 0;
                    id.Peer.Connection.BytesToRecieve = 4;
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerMessageLengthRecieved, id);
                    id.Peer.Connection.ProcessingQueue = false;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                cleanUp = true;
            }
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
        private void onPeerMessageLengthRecieved(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesRecieved = id.Peer.Connection.EndReceive(result);
                    if (bytesRecieved == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesRecieved += bytesRecieved;
                    if (id.Peer.Connection.BytesRecieved != id.Peer.Connection.BytesToRecieve)
                    {
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesRecieved, id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved, SocketFlags.None, peerMessageLengthRecieved, id);
                        return;
                    }

                    int networkOrderLength = BitConverter.ToInt32(id.Peer.Connection.recieveBuffer, 0);
                    id.Peer.Connection.BytesRecieved = 0;
                    id.Peer.Connection.BytesToRecieve = System.Net.IPAddress.NetworkToHostOrder(networkOrderLength);

                    id.Peer.Connection.LastMessageRecieved = DateTime.Now;
                    if (id.Peer.Connection.BytesToRecieve == 0)     // We recieved a KeepAlive
                    {
                        id.Peer.Connection.BytesToRecieve = 4;
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerMessageLengthRecieved, id);
                    }
                    else
                    {
                        lock (id.TorrentManager.downloadQueue)
                            id.TorrentManager.downloadQueue.Enqueue(id);
                        //int bytesRemaining = (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved);
                        //id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, bytesRemaining, SocketFlags.None, peerMessageRecieved, id);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
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
        private void onPeerMessageRecieved(IAsyncResult result)
        {
            bool cleanUp = false;
            PeerConnectionID id = (PeerConnectionID)result.AsyncState;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesRecieved = id.Peer.Connection.EndReceive(result);
                    if (bytesRecieved == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesRecieved += bytesRecieved;
                    id.TorrentManager.BytesDownloaded += bytesRecieved;
                    id.Peer.Connection.Monitor.BytesRecieved(bytesRecieved);

                    if (id.Peer.Connection.BytesRecieved != id.Peer.Connection.BytesToRecieve)
                    {
                        lock (id.TorrentManager.downloadQueue)
                            id.TorrentManager.downloadQueue.Enqueue(id);

                        return;
                    }

                    IPeerMessage message = PeerwireEncoder.Decode(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesRecieved, id.TorrentManager);
                    if(this.OnPeerMessages != null)
                    this.OnPeerMessages((IPeerConnectionID)id, new PeerMessageEventArgs(message, Direction.Incoming));
                    message.Handle(id); // FIXME: Is everything threadsafe here? Well, i know it isn't :p

                    if (!(message is PieceMessage))
                    {   // Only count Piecemessages as valid traffic (for the moment)
                        id.TorrentManager.BytesDownloaded -= message.ByteLength;
                        id.Peer.Connection.Monitor.BytesRecieved(-message.ByteLength);
                    }

                    id.Peer.Connection.LastMessageRecieved = DateTime.Now;

                    id.Peer.Connection.BytesRecieved = 0;
                    id.Peer.Connection.BytesToRecieve = 4;
                    id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, 0, id.Peer.Connection.BytesToRecieve, SocketFlags.None, peerMessageLengthRecieved, id);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
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
                lock (id)
                {
                    if (id.Peer.Connection == null)
                        return;

                    int bytesSent = id.Peer.Connection.EndSend(result);
                    if (bytesSent == 0)
                    {
                        cleanUp = true;
                        return;
                    }

                    id.Peer.Connection.BytesSent += bytesSent;
                    if (id.Peer.Connection.BytesToSend > 1024)
                    {   // Only counting piece messages
                        id.TorrentManager.BytesUploaded += bytesSent;
                        id.Peer.Connection.Monitor.BytesSent(bytesSent);
                    }

                    if (id.Peer.Connection.BytesSent != id.Peer.Connection.BytesToSend)
                    {
                        lock (id.TorrentManager.uploadQueue)
                            id.TorrentManager.uploadQueue.Enqueue(id);
                        return;
                    }

                    id.Peer.Connection.LastMessageSent = DateTime.Now;
                    this.ProcessQueue(id);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
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

            IPeerMessage msg = id.Peer.Connection.DeQueue();
            if (this.OnPeerMessages != null)
                this.OnPeerMessages((IPeerConnectionID)id, new PeerMessageEventArgs(msg, Direction.Outgoing));

            if (msg is PieceMessage)
                id.Peer.Connection.PiecesSent++;

            id.Peer.Connection.ProcessingQueue = true;
            try
            {
                id.Peer.Connection.BytesSent = 0;
                id.Peer.Connection.BytesToSend = msg.Encode(id.Peer.Connection.sendBuffer, 0);
                lock (id.TorrentManager.uploadQueue)
                    id.TorrentManager.uploadQueue.Enqueue(id);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                CleanupSocket(id);
            }
        }


        /// <summary>
        /// Makes a peer start downloading/uploading
        /// </summary>
        /// <param name="id">The peer to resume</param>
        /// <param name="downloading">True if you want to resume downloading, false if you want to resume uploading</param>
        public void ResumePeer(PeerConnectionID id, bool downloading)
        {
            bool cleanUp = false;

            try
            {
                lock (id)
                {
                    if (id.Peer.Connection == null)
                    {
                        cleanUp = true;
                        return;
                    }
                    if (downloading)
                    {
                        int bytesRemaining = (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToRecieve - id.Peer.Connection.BytesRecieved);
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesRecieved, bytesRemaining, SocketFlags.None, peerMessageRecieved, id);
                    }
                    else
                    {
                        int bytesRemaining = (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent) > ChunkLength ? ChunkLength : (id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent);
                        id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, bytesRemaining, SocketFlags.None, peerMessageSent, id);
                    }
                }
                return;
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    CleanupSocket(id);
            }
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

                        bytesSent = id.Peer.Connection.EndSend(result);
                        if (bytesSent == 0)
                        {
                            cleanUp = true;
                            return;
                        }

                        id.Peer.Connection.BytesSent += bytesSent;
                        if (bytesSent != id.Peer.Connection.BytesToSend)
                        {
                            id.Peer.Connection.BeginSend(id.Peer.Connection.sendBuffer, id.Peer.Connection.BytesSent, id.Peer.Connection.BytesToSend - id.Peer.Connection.BytesSent, SocketFlags.None, new AsyncCallback(this.IncomingConnectionAccepted), id);
                            return;
                        }

                        if (id.TorrentManager.ConnectedPeers.Contains(id) || id.TorrentManager.ConnectingTo.Contains(id))
                        {
                            id.Peer.Connection.Dispose();
                            return;
                        }

                        id.TorrentManager.Available.Remove(id);
                        id.TorrentManager.ConnectedPeers.Add(id);

                        id.Peer.Connection.BytesRecieved = 0;
                        id.Peer.Connection.BytesToRecieve = 4;
                        id.Peer.Connection.BeginReceive(id.Peer.Connection.recieveBuffer, id.Peer.Connection.BytesRecieved, id.Peer.Connection.BytesToRecieve, SocketFlags.None, this.peerMessageLengthRecieved, id);
                        
                        if (this.OnPeerConnectionChanged != null)
                            this.OnPeerConnectionChanged(id, new PeerConnectionEventArgs(PeerConnectionEvent.IncomingConnectionRecieved));
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.NativeErrorCode + ": " + ex.Message);
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
                    System.Threading.Interlocked.Decrement(ref openConnections);
                    id.TorrentManager.PieceManager.RemoveRequests(id);


                    if (id.Peer.Connection != null)
                    {
                        if (!id.Peer.Connection.AmChoking)
                            id.TorrentManager.UploadingTo--;
                        id.Peer.Connection.Dispose();
                        id.Peer.Connection = null;
                    }

                    id.TorrentManager.ConnectedPeers.Remove(id);
                    id.TorrentManager.ConnectingTo.Remove(id);
                    id.TorrentManager.Available.Add(id);

                    if (this.OnPeerConnectionChanged != null)
                        this.OnPeerConnectionChanged(id, new PeerConnectionEventArgs(PeerConnectionEvent.Disconnected));
                }
            }
        }
        #endregion
    }
}
