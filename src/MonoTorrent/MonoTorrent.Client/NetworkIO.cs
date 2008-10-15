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

        public bool ShouldAbort
        {
            get { return (Environment.TickCount - StartTime) > 10000; }
        }

        public AsyncConnect Callback;
        public IConnection Connection;
        public TorrentManager Manager;
        public Peer Peer;
        public IAsyncResult Result;
        public int StartTime;
    }

    internal static class NetworkIO
    {
        private static MonoTorrentCollection<AsyncIO> receiveQueue = new MonoTorrentCollection<AsyncIO>();
        private static MonoTorrentCollection<AsyncIO> sendQueue = new MonoTorrentCollection<AsyncIO>();

        private class AsyncIO
        {
            public AsyncIO(IConnection connection, byte[] buffer, int offset, int total, AsyncTransfer callback, object state, RateLimiter limiter)
            {
                Connection = connection;
                Buffer = buffer;
                Offset = offset;
                Count = 0;
                Callback = callback;
                RateLimiter = limiter;
                State = state;
                Total = total;
            }

            public byte[] Buffer;
            public AsyncTransfer Callback;
            public IConnection Connection;
            public int Count;
            public int Offset;
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
            EnqueueReceive(connection, buffer, offset, count, callback, state, null);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueReceive(connection, buffer, offset, count, callback, state, null);
        }

        internal static void EnqueueReceive(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter)
        {
            EnqueueReceive(connection, buffer.Array, buffer.Offset + offset, count, callback, state, limiter);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state, limiter);
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
                    // Receive in 2kB (or less) chunks to allow rate limiting to work
                    io.Connection.BeginReceive(io.Buffer, io.Offset + io.Count, Math.Min(ConnectionManager.ChunkLength, io.Total - io.Count), EndReceiveCallback, io);
                    if ((io.Total - io.Count) > ConnectionManager.ChunkLength / 2)
                        Interlocked.Decrement(ref io.RateLimiter.Chunks);
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
            EnqueueSend(connection, buffer, offset, count, callback, state, null);
        }

        internal static void EnqueueSend(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter)
        {
            EnqueueSend(connection, buffer.Array, buffer.Offset + offset, count, callback, state, limiter);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueSend(connection, buffer, offset, count, callback, state, null);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state, RateLimiter limiter)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state, limiter);
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
                    // Receive in 2kB (or less) chunks to allow rate limiting to work
                    io.Connection.BeginSend(io.Buffer, io.Offset + io.Count, Math.Min(ConnectionManager.ChunkLength, io.Total - io.Count), EndSendCallback, io);
                    if ((io.Total - io.Count) > ConnectionManager.ChunkLength / 2)
                        Interlocked.Decrement(ref io.RateLimiter.Chunks);
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
    }
}
