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
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.RateLimiters;

namespace MonoTorrent.Client
{
    class NetworkIO
    {
        static MainLoop IOLoop = new MainLoop ("NetworkIO Loop");

        public struct QueuedIO
        {
            public IConnection connection;
            public byte [] buffer;
            public int offset;
            public int count;
            public IRateLimiter rateLimiter;
            public TaskCompletionSource<int> tcs;

            public QueuedIO (IConnection connection, byte [] buffer, int offset, int count, IRateLimiter rateLimiter, TaskCompletionSource<int> tcs)
            {
                this.connection = connection;
                this.buffer = buffer;
                this.offset = offset;
                this.count = count;
                this.rateLimiter = rateLimiter;
                this.tcs = tcs;
            }
        }

        // The biggest message is a PieceMessage which is 16kB + some overhead
        // so send in chunks of 2kB + a little so we do 8 transfers per piece.
        const int ChunkLength = 2048 + 32;

        static readonly Queue<QueuedIO> receiveQueue = new Queue<QueuedIO> ();
        static readonly Queue<QueuedIO> sendQueue = new Queue<QueuedIO> ();

        static NetworkIO ()
        {
            IOLoop.QueueTimeout(TimeSpan.FromMilliseconds(100), delegate {
                while (receiveQueue.Count > 0) {
                    var io = receiveQueue.Peek ();
                    if (io.rateLimiter.TryProcess(io.count))
                        ReceiveQueuedAsync(receiveQueue.Dequeue());
                    else
                        break;
                }
                while (sendQueue.Count > 0) {
                    var io = sendQueue.Peek ();
                    if (io.rateLimiter.TryProcess(io.count))
                        SendQueuedAsync(sendQueue.Dequeue());
                    else
                        break;
                }

                return true;
            });
        }

        static async void ReceiveQueuedAsync (QueuedIO io)
        {
            try {
                var result = await io.connection.ReceiveAsync (io.buffer, io.offset, io.count).ConfigureAwait (false);
                io.tcs.SetResult (result);
            } catch (Exception ex) {
                io.tcs.SetException (ex);
            }
        }

        static async void SendQueuedAsync (QueuedIO io)
        {
            try {
                var result = await io.connection.SendAsync (io.buffer, io.offset, io.count).ConfigureAwait(false);
                io.tcs.SetResult (result);
            } catch (Exception ex) {
                io.tcs.SetException (ex);
            }
        }

        public static async Task ConnectAsync (IConnection connection)
        {
            await IOLoop;

            await connection.ConnectAsync ();
        }

        public static async Task ReceiveAsync(IConnection connection, byte [] buffer, int offset, int count, IRateLimiter rateLimiter, SpeedMonitor peerMonitor, SpeedMonitor managerMonitor)
        {
            await IOLoop;

            int remaining = count;
            while (remaining > 0) {
                int transferred;
                if (rateLimiter != null && !rateLimiter.Unlimited && !rateLimiter.TryProcess (Math.Min (ChunkLength, remaining))) {
                    var tcs = new TaskCompletionSource<int> ();
                    await IOLoop;
                    receiveQueue.Enqueue (new QueuedIO (connection, buffer, offset, Math.Min (ChunkLength, remaining), rateLimiter, tcs));
                    transferred = await tcs.Task.ConfigureAwait(false);
                } else {
                    transferred = await connection.ReceiveAsync(buffer, offset, remaining).ConfigureAwait(false); ;
                }

                if (transferred == 0)
                    throw new Exception("Socket is dead");

                peerMonitor?.AddDelta(transferred);
                managerMonitor?.AddDelta(transferred);

                offset += transferred;
                remaining -= transferred;
            } 
        }

        public static async Task SendAsync (IConnection connection, byte [] buffer, int offset, int count, IRateLimiter rateLimiter, SpeedMonitor peerMonitor, SpeedMonitor managerMonitor)
        {
            await IOLoop;

            int remaining = count;
            while (remaining > 0)
            {
                int transferred;
                if (rateLimiter != null && !rateLimiter.Unlimited && !rateLimiter.TryProcess (Math.Min (ChunkLength, remaining))) {
                    var tcs = new TaskCompletionSource<int> ();
                    await IOLoop;
                    sendQueue.Enqueue (new QueuedIO (connection, buffer, offset, Math.Min (ChunkLength, remaining), rateLimiter, tcs));
                    transferred = await tcs.Task.ConfigureAwait(false);
                } else {
                    transferred = await connection.SendAsync(buffer, offset, remaining).ConfigureAwait(false);
                }

                if (transferred == 0)
                    throw new Exception("Socket is dead");

                peerMonitor?.AddDelta(transferred);
                managerMonitor?.AddDelta(transferred);

                offset += transferred;
                remaining -= transferred;
            }
        }
    }
}
