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
using System.Threading;

using MonoTorrent.Client;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public delegate void AsyncIOCallback (bool succeeded, int transferred, object state);
    public delegate void AsyncMessageReceivedCallback (bool succeeded, PeerMessage message, object state);

    internal class AsyncConnectState
    {
        public AsyncConnectState(TorrentManager manager, Peer peer, IConnection connection)
        {
            Manager = manager;
            Peer = peer;
            Connection = connection;
        }

        public IConnection Connection;
        public TorrentManager Manager;
        public Peer Peer;
    }

    internal partial class NetworkIO
    {
        // The biggest message is a PieceMessage which is 16kB + some overhead
        // so send in chunks of 2kB + a little so we do 8 transfers per piece.
        const int ChunkLength = 2048 + 32;

        static Queue<AsyncIOState> receiveQueue = new Queue<AsyncIOState> ();
        static Queue<AsyncIOState> sendQueue = new Queue<AsyncIOState> ();

        static ICache <AsyncConnectState> connectCache = new Cache <AsyncConnectState> (true).Synchronize ();
        static ICache <AsyncIOState> transferCache = new Cache <AsyncIOState> (true).Synchronize ();

        static AsyncCallback EndConnectCallback = EndConnect;
        static AsyncCallback EndReceiveCallback = EndReceive;
        static AsyncCallback EndSendCallback = EndSend;

        static NetworkIO()
        {
            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(100), delegate {
                lock (sendQueue)
                {
                    int count = sendQueue.Count;
                    for (int i = 0; i < count; i++)
                         SendOrEnqueue (sendQueue.Dequeue ());
                }
                lock (receiveQueue)
                {
                    int count = receiveQueue.Count;
                    for (int i = 0; i < count; i++)
                        ReceiveOrEnqueue (receiveQueue.Dequeue ());
                }
                return true;
            });
        }

        static int halfOpens;
        public static int HalfOpens {
            get { return halfOpens; }
        }

        public static void EnqueueConnect (IConnection connection, AsyncIOCallback callback, object state)
        {
            var data = connectCache.Dequeue ().Initialise (connection, callback, state);

            try {
                var result = connection.BeginConnect (EndConnectCallback, data);
                Interlocked.Increment (ref halfOpens);
                ClientEngine.MainLoop.QueueTimeout (TimeSpan.FromSeconds (10), delegate {
                    if (!result.IsCompleted)
                        connection.Dispose ();
                    return false;
                });
            } catch {
                callback (false, 0, state);
                connectCache.Enqueue (data);
            }
        }

        public static void EnqueueReceive (IConnection connection, byte[] buffer, int offset, int count, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, AsyncIOCallback callback, object state)
        {
            var data = transferCache.Dequeue ().Initialise (connection, buffer, offset, count, callback, state, rateLimiter, peerMonitor, managerMonitor);
            lock (receiveQueue)
                ReceiveOrEnqueue (data);
        }

        public static void EnqueueSend (IConnection connection, byte[] buffer, int offset, int count, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, AsyncIOCallback callback, object state)
        {
            var data = transferCache.Dequeue ().Initialise (connection, buffer, offset, count, callback, state, rateLimiter, peerMonitor, managerMonitor);
            lock (sendQueue)
                SendOrEnqueue (data);
        }

        static void EndConnect (IAsyncResult result)
        {
            var data = (AsyncConnectState) result.AsyncState;
            try {
                Interlocked.Decrement (ref halfOpens);
                data.Connection.EndConnect (result);
                data.Callback (true, 0, data.State);
            } catch {
                data.Callback (false, 0, data.State);
            } finally {
                connectCache.Enqueue (data);
            }
        }

        static void EndReceive (IAsyncResult result)
        {
            var data = (AsyncIOState) result.AsyncState;
            try {
                int transferred = data.Connection.EndReceive (result);
                if (transferred == 0) {
                    data.Callback (false, 0, data.State);
                    transferCache.Enqueue (data);
                } else {
                    if (data.PeerMonitor != null)
                        data.PeerMonitor.BytesReceived (transferred, data.TransferType);
                    if (data.ManagerMonitor != null)
                        data.ManagerMonitor.BytesReceived (transferred, data.TransferType);

                    data.Offset += transferred;
                    data.Remaining -= transferred;
                    if (data.Remaining == 0) {
                        data.Callback (true, data.Count, data.State);
                        transferCache.Enqueue (data);
                    } else {
                        lock (receiveQueue)
                            ReceiveOrEnqueue (data);
                    }
                }
            } catch {
                data.Callback (false, 0, data.State);
                transferCache.Enqueue (data);
            }
        }

        static void EndSend (IAsyncResult result)
        {
            var data = (AsyncIOState) result.AsyncState;
            try {
                int transferred = data.Connection.EndSend (result);
                if (transferred == 0) {
                    data.Callback (false, 0, data.State);
                    transferCache.Enqueue (data);
                } else {
                    if (data.PeerMonitor != null)
                        data.PeerMonitor.BytesSent (transferred, data.TransferType);
                    if (data.ManagerMonitor != null)
                        data.ManagerMonitor.BytesSent (transferred, data.TransferType);

                    data.Offset += transferred;
                    data.Remaining -= transferred;
                    if (data.Remaining == 0) {
                        data.Callback (true, data.Count, data.State);
                        transferCache.Enqueue (data);
                    } else {
                        lock (sendQueue)
                            SendOrEnqueue (data);
                    }
                }
            } catch {
                data.Callback (false, 0, data.State);
                transferCache.Enqueue (data);
            }
        }

        static void ReceiveOrEnqueue (AsyncIOState data)
        {
            int count = Math.Min (ChunkLength, data.Remaining);
            if (data.RateLimiter == null || data.RateLimiter.TryProcess (1)) {
                try {
                    data.Connection.BeginReceive (data.Buffer, data.Offset, count, EndReceiveCallback, data);
                } catch {
                    data.Callback (false, 0, data.State);
                    transferCache.Enqueue (data);
                }
            } else {
                receiveQueue.Enqueue (data);
            }
        }

        static void SendOrEnqueue (AsyncIOState data)
        {
            int count = Math.Min (ChunkLength, data.Remaining);
            if (data.RateLimiter == null || data.RateLimiter.TryProcess (1)) {
                try {
                    data.Connection.BeginSend (data.Buffer, data.Offset, count, EndSendCallback, data);
                } catch {
                    data.Callback (false, 0, data.State);
                    transferCache.Enqueue (data);
                }
            } else {
                sendQueue.Enqueue (data);
            }
        }
    }
}
