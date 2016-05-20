using System;
using System.Collections.Generic;
using System.Threading;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public delegate void AsyncIOCallback(bool succeeded, int transferred, object state);

    public delegate void AsyncMessageReceivedCallback(bool succeeded, PeerMessage message, object state);

    internal class AsyncConnectState
    {
        public IConnection Connection;
        public TorrentManager Manager;
        public Peer Peer;

        public AsyncConnectState(TorrentManager manager, Peer peer, IConnection connection)
        {
            Manager = manager;
            Peer = peer;
            Connection = connection;
        }
    }

    internal partial class NetworkIO
    {
        // The biggest message is a PieceMessage which is 16kB + some overhead
        // so send in chunks of 2kB + a little so we do 8 transfers per piece.
        private const int ChunkLength = 2048 + 32;

        private static readonly Queue<AsyncIOState> receiveQueue = new Queue<AsyncIOState>();
        private static readonly Queue<AsyncIOState> sendQueue = new Queue<AsyncIOState>();

        private static readonly ICache<AsyncConnectState> connectCache =
            new Cache<AsyncConnectState>(true).Synchronize();

        private static readonly ICache<AsyncIOState> transferCache = new Cache<AsyncIOState>(true).Synchronize();

        private static readonly AsyncCallback EndConnectCallback = EndConnect;
        private static readonly AsyncCallback EndReceiveCallback = EndReceive;
        private static readonly AsyncCallback EndSendCallback = EndSend;

        private static int halfOpens;

        static NetworkIO()
        {
            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(100), delegate
            {
                lock (sendQueue)
                {
                    var count = sendQueue.Count;
                    for (var i = 0; i < count; i++)
                        SendOrEnqueue(sendQueue.Dequeue());
                }
                lock (receiveQueue)
                {
                    var count = receiveQueue.Count;
                    for (var i = 0; i < count; i++)
                        ReceiveOrEnqueue(receiveQueue.Dequeue());
                }
                return true;
            });
        }

        public static int HalfOpens
        {
            get { return halfOpens; }
        }

        public static void EnqueueConnect(IConnection connection, AsyncIOCallback callback, object state)
        {
            var data = connectCache.Dequeue().Initialise(connection, callback, state);

            try
            {
                var result = connection.BeginConnect(EndConnectCallback, data);
                Interlocked.Increment(ref halfOpens);
                ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(10), delegate
                {
                    if (!result.IsCompleted)
                        connection.Dispose();
                    return false;
                });
            }
            catch
            {
                callback(false, 0, state);
                connectCache.Enqueue(data);
            }
        }

        public static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count,
            IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor,
            AsyncIOCallback callback, object state)
        {
            var data = transferCache.Dequeue()
                .Initialise(connection, buffer, offset, count, callback, state, rateLimiter, peerMonitor, managerMonitor);
            lock (receiveQueue)
                ReceiveOrEnqueue(data);
        }

        public static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count,
            IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor,
            AsyncIOCallback callback, object state)
        {
            var data = transferCache.Dequeue()
                .Initialise(connection, buffer, offset, count, callback, state, rateLimiter, peerMonitor, managerMonitor);
            lock (sendQueue)
                SendOrEnqueue(data);
        }

        private static void EndConnect(IAsyncResult result)
        {
            var data = (AsyncConnectState) result.AsyncState;
            try
            {
                Interlocked.Decrement(ref halfOpens);
                data.Connection.EndConnect(result);
                data.Callback(true, 0, data.State);
            }
            catch
            {
                data.Callback(false, 0, data.State);
            }
            finally
            {
                connectCache.Enqueue(data);
            }
        }

        private static void EndReceive(IAsyncResult result)
        {
            var data = (AsyncIOState) result.AsyncState;
            try
            {
                var transferred = data.Connection.EndReceive(result);
                if (transferred == 0)
                {
                    data.Callback(false, 0, data.State);
                    transferCache.Enqueue(data);
                }
                else
                {
                    if (data.PeerMonitor != null)
                        data.PeerMonitor.BytesReceived(transferred, data.TransferType);
                    if (data.ManagerMonitor != null)
                        data.ManagerMonitor.BytesReceived(transferred, data.TransferType);

                    data.Offset += transferred;
                    data.Remaining -= transferred;
                    if (data.Remaining == 0)
                    {
                        data.Callback(true, data.Count, data.State);
                        transferCache.Enqueue(data);
                    }
                    else
                    {
                        lock (receiveQueue)
                            ReceiveOrEnqueue(data);
                    }
                }
            }
            catch
            {
                data.Callback(false, 0, data.State);
                transferCache.Enqueue(data);
            }
        }

        private static void EndSend(IAsyncResult result)
        {
            var data = (AsyncIOState) result.AsyncState;
            try
            {
                var transferred = data.Connection.EndSend(result);
                if (transferred == 0)
                {
                    data.Callback(false, 0, data.State);
                    transferCache.Enqueue(data);
                }
                else
                {
                    if (data.PeerMonitor != null)
                        data.PeerMonitor.BytesSent(transferred, data.TransferType);
                    if (data.ManagerMonitor != null)
                        data.ManagerMonitor.BytesSent(transferred, data.TransferType);

                    data.Offset += transferred;
                    data.Remaining -= transferred;
                    if (data.Remaining == 0)
                    {
                        data.Callback(true, data.Count, data.State);
                        transferCache.Enqueue(data);
                    }
                    else
                    {
                        lock (sendQueue)
                            SendOrEnqueue(data);
                    }
                }
            }
            catch
            {
                data.Callback(false, 0, data.State);
                transferCache.Enqueue(data);
            }
        }

        private static void ReceiveOrEnqueue(AsyncIOState data)
        {
            var count = Math.Min(ChunkLength, data.Remaining);
            if (data.RateLimiter == null || data.RateLimiter.TryProcess(1))
            {
                try
                {
                    data.Connection.BeginReceive(data.Buffer, data.Offset, count, EndReceiveCallback, data);
                }
                catch
                {
                    data.Callback(false, 0, data.State);
                    transferCache.Enqueue(data);
                }
            }
            else
            {
                receiveQueue.Enqueue(data);
            }
        }

        private static void SendOrEnqueue(AsyncIOState data)
        {
            var count = Math.Min(ChunkLength, data.Remaining);
            if (data.RateLimiter == null || data.RateLimiter.TryProcess(1))
            {
                try
                {
                    data.Connection.BeginSend(data.Buffer, data.Offset, count, EndSendCallback, data);
                }
                catch
                {
                    data.Callback(false, 0, data.State);
                    transferCache.Enqueue(data);
                }
            }
            else
            {
                sendQueue.Enqueue(data);
            }
        }
    }
}