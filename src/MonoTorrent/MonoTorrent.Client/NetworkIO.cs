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
        static ICache <AsyncConnectState> connectCache = new Cache <AsyncConnectState> (true).Synchronize ();
        static ICache <AsyncIOState> transferCache = new Cache <AsyncIOState> (true).Synchronize ();

        static AsyncCallback EndConnectCallback = EndConnect;
        static AsyncCallback EndReceiveCallback = EndReceive;
        static AsyncCallback EndSendCallback = EndSend;

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
            try {
                connection.BeginReceive (buffer, offset, count, EndReceiveCallback, data);
            } catch {
                data.Callback (false, 0, state);
                transferCache.Enqueue (data);
            }
        }

        public static void EnqueueSend (IConnection connection, byte[] buffer, int offset, int count, IRateLimiter rateLimiter, ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor, AsyncIOCallback callback, object state)
        {
            var data = transferCache.Dequeue ().Initialise (connection, buffer, offset, count, callback, state, rateLimiter, peerMonitor, managerMonitor);
            try {
                connection.BeginSend (buffer, offset, count, EndSendCallback, data);
            } catch {
                callback (false, 0, state);
                transferCache.Enqueue (data);
            }
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
                data.Offset += transferred;
                data.Remaining -= transferred;
                if (data.Remaining == 0) {
                    data.Callback (true, data.Count, data.State);
                    transferCache.Enqueue (data);
                } else {
                    data.Connection.BeginReceive (data.Buffer, data.Offset, data.Remaining, EndReceiveCallback, data);
                }
            } catch (Exception ex) {
                data.Callback (false, 0, data.State);
                transferCache.Enqueue (data);
            }
        }

        static void EndSend (IAsyncResult result)
        {
            var data = (AsyncIOState) result.AsyncState;
            try {
                int transferred = data.Connection.EndSend (result);
                data.Offset += transferred;
                data.Remaining -= transferred;
                if (data.Remaining == 0) {
                    data.Callback (true, data.Count, data.State);
                    transferCache.Enqueue (data);
                } else {
                    data.Connection.BeginSend (data.Buffer, data.Offset, data.Remaining, EndSendCallback, data);
                }
            } catch {
                data.Callback (false, 0, data.State);
                transferCache.Enqueue (data);
            }
        }
    }
}
