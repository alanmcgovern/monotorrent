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

using MonoTorrent.Client.RateLimiters;
using MonoTorrent.Connections.Peer;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class NetworkIO
    {
        internal static MemoryPool BufferPool { get; } = MemoryPool.Default;

        public struct QueuedIO
        {
            public IPeerConnection connection;
            public Memory<byte> buffer;
            public IRateLimiter rateLimiter;
            public ReusableTaskCompletionSource<int> tcs;

            public QueuedIO (IPeerConnection connection, Memory<byte> buffer, IRateLimiter rateLimiter, ReusableTaskCompletionSource<int> tcs)
            {
                this.connection = connection;
                this.buffer = buffer;
                this.rateLimiter = rateLimiter;
                this.tcs = tcs;
            }
        }

        // The biggest message is a PieceMessage which is 16kB + some overhead
        // so send in chunks of 2kB + a little so we do 8 transfers per piece.
        internal const int ChunkLength = 2048 + 32;

        static readonly Queue<QueuedIO> receiveQueue = new Queue<QueuedIO> ();
        static readonly Queue<QueuedIO> sendQueue = new Queue<QueuedIO> ();

        static NetworkIO ()
        {
            ClientEngine.MainLoop.QueueTimeout (TimeSpan.FromMilliseconds (100), delegate {
                lock (receiveQueue) {
                    while (receiveQueue.Count > 0) {
                        QueuedIO io = receiveQueue.Peek ();
                        if (io.rateLimiter.TryProcess (io.buffer.Length))
                            ReceiveQueuedAsync (receiveQueue.Dequeue ());
                        else
                            break;
                    }
                }
                lock (sendQueue) {
                    while (sendQueue.Count > 0) {
                        QueuedIO io = sendQueue.Peek ();
                        if (io.rateLimiter.TryProcess (io.buffer.Length))
                            SendQueuedAsync (sendQueue.Dequeue ());
                        else
                            break;
                    }
                }

                return true;
            });
        }

        static async void ReceiveQueuedAsync (QueuedIO io)
        {
            try {
                int result = await io.connection.ReceiveAsync (io.buffer).ConfigureAwait (false);
                io.tcs.SetResult (result);
            } catch (Exception ex) {
                io.tcs.SetException (ex);
            }
        }

        static async void SendQueuedAsync (QueuedIO io)
        {
            try {
                int result = await io.connection.SendAsync (io.buffer).ConfigureAwait (false);
                io.tcs.SetResult (result);
            } catch (Exception ex) {
                io.tcs.SetException (ex);
            }
        }

        public static async ReusableTask ConnectAsync (IPeerConnection connection)
        {
            await MainLoop.SwitchToThreadpool ();

            await connection.ConnectAsync ();
        }

        public static ReusableTask ReceiveAsync (IPeerConnection connection, Memory<byte> buffer)
        {
            return ReceiveAsync (connection, buffer, null, null, null);
        }

        public static async ReusableTask ReceiveAsync (IPeerConnection connection, Memory<byte> buffer, IRateLimiter? rateLimiter, SpeedMonitor? peerMonitor, SpeedMonitor? managerMonitor)
        {
            await MainLoop.SwitchToThreadpool ();

            while (buffer.Length > 0) {
                int transferred;
                bool unlimited = rateLimiter?.Unlimited ?? true;

                if (rateLimiter != null && !unlimited && !rateLimiter.TryProcess (buffer.Length)) {
                    var tcs = new ReusableTaskCompletionSource<int> ();
                    lock (receiveQueue)
                        receiveQueue.Enqueue (new QueuedIO (connection, buffer, rateLimiter, tcs));
                    transferred = await tcs.Task.ConfigureAwait (false);
                } else {
                    transferred = await connection.ReceiveAsync (buffer).ConfigureAwait (false);
                }

                if (transferred == 0)
                    throw new ConnectionClosedException ("Socket receive returned 0, indicating the connection has been closed.");

                peerMonitor?.AddDelta (transferred);
                managerMonitor?.AddDelta (transferred);

                buffer = buffer.Slice (transferred);
            }
        }

        public static ReusableTask SendAsync (IPeerConnection connection, Memory<byte> buffer)
        {
            return SendAsync (connection, buffer, null, null, null);
        }

        public static async ReusableTask SendAsync (IPeerConnection connection, Memory<byte> buffer, IRateLimiter? rateLimiter, SpeedMonitor? peerMonitor, SpeedMonitor? managerMonitor)
        {
            await MainLoop.SwitchToThreadpool ();

            while (buffer.Length > 0) {
                int transferred;
                bool unlimited = rateLimiter?.Unlimited ?? true;

                if (rateLimiter != null && !unlimited && !rateLimiter.TryProcess (buffer.Length)) {
                    var tcs = new ReusableTaskCompletionSource<int> ();
                    lock (sendQueue)
                        sendQueue.Enqueue (new QueuedIO (connection, buffer, rateLimiter, tcs));
                    transferred = await tcs.Task.ConfigureAwait (false);
                } else {
                    transferred = await connection.SendAsync (buffer).ConfigureAwait (false);
                }

                if (transferred == 0)
                    throw new ConnectionClosedException ("Socket send returned 0, indicating the connection has been closed.");

                peerMonitor?.AddDelta (transferred);
                managerMonitor?.AddDelta (transferred);

                buffer = buffer.Slice (transferred);
            }
        }
    }
}
