//
// NetworkIO.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;
using System.Net;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    internal delegate void AsyncConnect(bool succeeded, object state);
    internal delegate void AsyncTransfer(bool succeeded, int count, object state);

    internal class AsyncConnectState
    {
        public AsyncConnectState(TorrentManager manager, Peer peer, IConnection connection, AsyncConnect callback)
        {
            Manager = manager;
            Peer = peer;
            Connection = connection;
            Callback = callback;
        }

        public AsyncConnect Callback;
        public IConnection Connection;
        public TorrentManager Manager;
        public Peer Peer;
        public IAsyncResult Result;
    }

    internal static class NetworkIO
    {
        private static MonoTorrentCollection<AsyncIO> receiveQueue = new MonoTorrentCollection<AsyncIO>();
        private static MonoTorrentCollection<AsyncIO> sendQueue = new MonoTorrentCollection<AsyncIO>();

        private class AsyncIO
        {
            public AsyncIO(IConnection connection, byte[] buffer, int offset, int total, AsyncTransfer callback, object state, RateLimiter limiter, ConnectionMonitor managerMonitor, ConnectionMonitor peerMonitor)
            {
                Connection = connection;
                Buffer = buffer;
                Offset = offset;
                Count = 0;
                Callback = callback;
                ManagerMonitor = managerMonitor;
                PeerMonitor = peerMonitor;
                RateLimiter = limiter;
                State = state;
                Total = total;
            }

            public byte[] Buffer;
            public AsyncTransfer Callback;
            public IConnection Connection;
            public ConnectionMonitor ManagerMonitor;
            public int Count;
            public int Offset;
            public ConnectionMonitor PeerMonitor;
            public RateLimiter RateLimiter;
            public object State;
            public int Total;
        }

        static readonly AsyncCallback EndReceiveCallback = EndReceive;
        static readonly AsyncCallback EndSendCallback = EndSend;

        static NetworkIO()
        {
            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(50), delegate {
                lock (sendQueue)
                {
                    for (int i = 0; i < sendQueue.Count;)
                    {
                        if (sendQueue[i].RateLimiter.Chunks > 0)
                        {
                            EnqueueSend(sendQueue[i]);
                            sendQueue.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                lock (receiveQueue)
                {
                    for (int i = 0; i < receiveQueue.Count;)
                    {
                        if (receiveQueue[i].RateLimiter.Chunks > 0)
                        {
                            EnqueueReceive(receiveQueue[i]);
                            receiveQueue.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                return true;
            });
        }

        private static int halfOpens;

        public static int HalfOpens
        {
            get { return halfOpens; }
        }

        private static void EndConnect(IAsyncResult result)
        {
            Interlocked.Decrement(ref halfOpens);
            bool succeeded = true;
            AsyncConnectState c = (AsyncConnectState)result.AsyncState;

            try
            {
                c.Connection.EndConnect(result);
            }
            catch
            {
                succeeded = false;
            }

            c.Callback(succeeded, c);
        }

        internal static void EndReceive(IAsyncResult result)
        {
            AsyncIO io = (AsyncIO)result.AsyncState;

            try
            {
                int count = io.Connection.EndReceive(result);
                io.Count += count;
                if(io.PeerMonitor != null)
                    io.PeerMonitor.BytesReceived(count, io.Total > Piece.BlockSize / 2 ? TransferType.Data : TransferType.Protocol);
                if (io.ManagerMonitor != null)
                    io.ManagerMonitor.BytesReceived(count, io.Total > Piece.BlockSize / 2 ? TransferType.Data : TransferType.Protocol);
                
                if (count > 0 && io.Count < io.Total)
                {
                    EnqueueReceive(io);
                    return;
                }
            }
            catch
            {
                // No need to do anything, io.Count != io.Total, so it'll fail
            }

            io.Callback(io.Count == io.Total, io.Count, io.State);
        }

        internal static void EndSend(IAsyncResult result)
        {
            AsyncIO io = (AsyncIO)result.AsyncState;

            try
            {
                int count = io.Connection.EndSend(result);
                io.Count += count;
                if(io.PeerMonitor != null)
                    io.PeerMonitor.BytesSent(count, io.Total > Piece.BlockSize / 2 ? TransferType.Data : TransferType.Protocol);
                if (io.ManagerMonitor != null)
                    io.ManagerMonitor.BytesSent(count, io.Total > Piece.BlockSize / 2 ? TransferType.Data : TransferType.Protocol);

                if (count > 0 && io.Count < io.Total)
                {
                    EnqueueSend(io);
                    return;
                }
            }
            catch
            {
                // No need to do anything, io.Count != io.Total, so it'll fail
            }

            io.Callback(io.Count == io.Total, io.Count, io.State);
        }

        internal static void EnqueueConnect(AsyncConnectState c)
        {
            Interlocked.Increment(ref halfOpens);
            try
            {
                c.Result = c.Connection.BeginConnect(EndConnect, c);
                ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(10), delegate {
                    if (!c.Result.IsCompleted)
                        c.Connection.Dispose();
                    return false;
                });
            }
            catch
            {
                c.Callback(false, c);
            }
        }

        internal static void EnqueueReceive(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueReceive(connection, buffer, offset, count, callback, state, null, null, null);
        }

        internal static void EnqueueReceive(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter, ConnectionMonitor managerMonitor, ConnectionMonitor peerMonitor)
        {
            EnqueueReceive(connection, buffer.Array, buffer.Offset + offset, count, callback, state, limiter, managerMonitor, peerMonitor);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueReceive(connection, buffer, offset, count, callback, state, null, null, null);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter, ConnectionMonitor managerMonitor, ConnectionMonitor peerMonitor)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state, limiter, managerMonitor, peerMonitor);
            EnqueueReceive(io);
        }

        private static void EnqueueReceive(AsyncIO io)
        {
            try
            {
                if (io.RateLimiter == null)
                {
                    io.Connection.BeginReceive(io.Buffer, io.Offset + io.Count, io.Total - io.Count, EndReceiveCallback, io);
                }
                else if (io.RateLimiter.Chunks > 0)
                {
                    if ((io.Total - io.Count) > ConnectionManager.ChunkLength / 2)
                        Interlocked.Decrement(ref io.RateLimiter.Chunks);

                    // Receive in 2kB (or less) chunks to allow rate limiting to work
                    io.Connection.BeginReceive(io.Buffer, io.Offset + io.Count, Math.Min(ConnectionManager.ChunkLength, io.Total - io.Count), EndReceiveCallback, io);
                }
                else
                {
                    lock (receiveQueue)
                        receiveQueue.Add(io);
                }
            }
            catch
            {
                io.Callback(false, 0, io.State);
            }
        }

        internal static void EnqueueSend(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueSend(connection, buffer, offset, count, callback, state, null, null, null);
        }

        internal static void EnqueueSend(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter, ConnectionMonitor managerMonitor, ConnectionMonitor peerMonitor)
        {
            EnqueueSend(connection, buffer.Array, buffer.Offset + offset, count, callback, state, limiter, managerMonitor, peerMonitor);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueSend(connection, buffer, offset, count, callback, state, null, null, null);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter, ConnectionMonitor managerMonitor, ConnectionMonitor peerMonitor)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state, limiter, managerMonitor, peerMonitor);
            EnqueueSend(io);
        }

        private static void EnqueueSend(AsyncIO io)
        {
            try
            {
                if (io.RateLimiter == null)
                {
                    io.Connection.BeginSend(io.Buffer, io.Offset + io.Count, io.Total - io.Count, EndSendCallback, io);
                }
                else if (io.RateLimiter.Chunks > 0)
                {
                    if ((io.Total - io.Count) > ConnectionManager.ChunkLength / 2)
                        Interlocked.Decrement(ref io.RateLimiter.Chunks);

                    // Receive in 2kB (or less) chunks to allow rate limiting to work
                    io.Connection.BeginSend(io.Buffer, io.Offset + io.Count, Math.Min(ConnectionManager.ChunkLength, io.Total - io.Count), EndSendCallback, io);
                }
                else
                {
                    lock (sendQueue)
                        sendQueue.Add(io);
                }
            }
            catch
            {
                io.Callback(false, 0, io.State);
            }
        }


        public static void ReceiveMessage(PeerId id)
        {
            IConnection connection = id.Connection;
            if (connection == null)
                return;

            ClientEngine.BufferManager.GetBuffer(ref id.recieveBuffer, 4);
            RateLimiter limiter = id.Engine.Settings.GlobalMaxDownloadSpeed > 0 ? id.Engine.downloadLimiter : null;
            limiter = limiter == null && id.TorrentManager.Settings.MaxDownloadSpeed > 0 ? id.TorrentManager.downloadLimiter : null;
            EnqueueReceive(connection, id.recieveBuffer, 0, 4, MessageLengthReceived, id, limiter, id.TorrentManager.Monitor, id.Monitor);
        }

        static void MessageLengthReceived(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;
            if (!succeeded)
            {
                id.ConnectionManager.CleanupSocket(id, "Couldn't receive message length");
            }
            else
            {
                IConnection connection = id.Connection;
                if (connection == null)
                    return;

                // Decode the message length from the buffer. It is a big endian integer, so make sure
                // it is converted to host endianness.
                id.Decryptor.Decrypt(id.recieveBuffer.Array, id.recieveBuffer.Offset, count);
                int messageBodyLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(id.recieveBuffer.Array, id.recieveBuffer.Offset));


                // If bytes to receive is zero, it means we received a keep alive message
                // so we just start receiving a new message length again
                if (messageBodyLength == 0)
                {
                    id.LastMessageReceived = DateTime.Now;
                    ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                    ReceiveMessage(id);
                }

                // Otherwise queue the peer in the Receive buffer and try to resume downloading off him
                else
                {
                    ArraySegment<byte> buffer = BufferManager.EmptyBuffer;
                    ClientEngine.BufferManager.GetBuffer(ref buffer, messageBodyLength + 4);
                    Buffer.BlockCopy(id.recieveBuffer.Array, id.recieveBuffer.Offset, buffer.Array, buffer.Offset, 4);
                    
                    ClientEngine.BufferManager.FreeBuffer(ref id.recieveBuffer);
                    id.recieveBuffer = buffer;
                    RateLimiter limiter = id.Engine.Settings.GlobalMaxDownloadSpeed > 0 ? id.Engine.downloadLimiter : null;
                    limiter = limiter == null && id.TorrentManager.Settings.MaxDownloadSpeed > 0 ? id.TorrentManager.downloadLimiter : null;
                    EnqueueReceive(connection, id.recieveBuffer, 4, messageBodyLength, MessageBodyReceived, id, limiter, id.TorrentManager.Monitor, id.Monitor);
                }
            }
        }

        static void MessageBodyReceived(bool succeeded, int count, object state)
        {
            PeerId id = (PeerId)state;

            if (!succeeded)
            {
                id.ConnectionManager.CleanupSocket(id, "Couldn't receive message body");
            }
            else
            {
                // The first 4 bytes are the already decrypted message length
                id.Decryptor.Decrypt(id.recieveBuffer.Array, id.recieveBuffer.Offset + 4, count);

                ArraySegment<byte> buffer = id.recieveBuffer;
                id.recieveBuffer = BufferManager.EmptyBuffer;
                ClientEngine.MainLoop.Queue(delegate {
                    ProcessMessage(id, buffer, count);
                });
                
                // Receive the next message
                ReceiveMessage(id);
            }
        }

        private static void ProcessMessage(PeerId id, ArraySegment<byte> buffer, int count)
        {
            string reason = "";
            bool cleanUp = false;
            try
            {
                try
                {
                    PeerMessage message = PeerMessage.DecodeMessage(buffer, 0, 4 + count, id.TorrentManager);

                    // Fire the event to say we recieved a new message
                    PeerMessageEventArgs e = new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)message, Direction.Incoming, id);
                    id.ConnectionManager.RaisePeerMessageTransferred(e);

                    message.Handle(id);
                }
                catch (Exception ex)
                {
                    // Should i nuke the peer with the dodgy message too?
                    Logger.Log(null, "*CRITICAL EXCEPTION* - Error decoding message: {0}", ex);
                }
                finally
                {
                    ClientEngine.BufferManager.FreeBuffer(ref buffer);
                }


                //FIXME: I thought i was using 5 (i changed the check below from 3 to 5)...
                // if the peer has sent us three bad pieces, we close the connection.
                if (id.Peer.TotalHashFails == 5)
                {
                    reason = "5 hashfails";
                    Logger.Log(id.Connection, "ConnectionManager - 5 hashfails");
                    cleanUp = true;
                    return;
                }

                id.LastMessageReceived = DateTime.Now;
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
                    id.ConnectionManager.CleanupSocket(id, reason);
            }
        }
    }
}
