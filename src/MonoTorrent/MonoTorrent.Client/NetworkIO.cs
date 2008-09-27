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
        private class AsyncIO
        {
            public AsyncIO(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
            {
                Connection = connection;
                Buffer = buffer;
                Offset = offset;
                Count = count;
                Callback = callback;
                State = state;
            }

            public byte[] Buffer;
            public AsyncTransfer Callback;
            public IConnection Connection;
            public int Count;
            public int Offset;
            public object State;
        }

        static readonly AsyncCallback EndReceiveCallback = EndReceive;
        static readonly AsyncCallback EndSendCallback = EndSend;

        private static int halfOpens;

        public static int HalfOpens
        {
            get { return halfOpens; }
        }

        private static void EndConnect(IAsyncResult result)
        {
            Interlocked.Decrement(ref halfOpens);
            ClientEngine.MainLoop.Queue(delegate
            {
                bool succeeded = true;
                AsyncConnectState c = (AsyncConnectState)result.AsyncState;

                try
                {
                    c.Connection.EndConnect(c.Result);
                }
                catch
                {
                    succeeded = false;
                }
                finally
                {
                    c.Result.AsyncWaitHandle.Close();
                }

                c.Callback(succeeded, c);
            });
        }

        internal static void EndReceive(IAsyncResult result)
        {
            int count = 0;
            bool succeeded = true;
            AsyncIO io = (AsyncIO)result.AsyncState;

            try
            {
                count = io.Connection.EndReceive(result);
            }
            catch
            {
                succeeded = false;
            }

            io.Callback(succeeded && count > 0, count, io.State);
        }

        internal static void EndSend(IAsyncResult result)
        {
            int count = 0;
            bool succeeded = true;
            AsyncIO io = (AsyncIO)result.AsyncState;

            try
            {
                count = io.Connection.EndSend(result);
            }
            catch
            {
                succeeded = false;
            }

            io.Callback(succeeded && count > 0, count, io.State);
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
            EnqueueReceive(connection, buffer.Array, buffer.Offset + offset, count, callback, state);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state);
            try
            {
                connection.BeginReceive(buffer, offset, count, EndReceiveCallback, io);
            }
            catch
            {
                callback(false, 0, state);
            }
        }

        internal static void EnqueueSend(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueSend(connection, buffer.Array, buffer.Offset + offset, count, callback, state);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            AsyncIO io = new AsyncIO(connection, buffer, offset, count, callback, state);
            try
            {
                connection.BeginSend(buffer, offset, count, EndSendCallback, io);
            }
            catch
            {
                callback(false, 0, state);
            }
        }
    }
}
