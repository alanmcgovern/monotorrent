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

        static List<AsyncIO> sends = new List<AsyncIO>();
        static List<AsyncIO> receives = new List<AsyncIO>();
        static List<AsyncConnectState> pendingConnects = new List<AsyncConnectState>();

        static ManualResetEvent handle;
        static object locker = new object();
        static List<AsyncConnectState> connects;

        public static int HalfOpens
        {
            get { lock (locker) return connects.Count; }
        }

        static NetworkIO()
        {
            locker = new object();
            connects = new List<AsyncConnectState>();
            handle = new ManualResetEvent(false);

            Thread t = new Thread((ThreadStart)delegate
            {
                while (true)
                {
                    int waitTime = 1;
                    AsyncConnectState beginConnect= null;
                    AsyncConnectState c = null;
                    AsyncIO r = null;
                    AsyncIO s = null;

                    lock (locker)
                    {
                        if (pendingConnects.Count > 0)
                        {
                            beginConnect = pendingConnects[0];
                            pendingConnects.RemoveAt(0);
                        }

                        if (receives.Count > 0)
                        {
                            r = receives[0];
                            receives.RemoveAt(0);
                        }

                        if (sends.Count > 0)
                        {
                            s = sends[0];
                            sends.RemoveAt(0);
                        }

                        for (int i = 0; i < connects.Count; i++)
                        {
                            if (!connects[i].Result.IsCompleted && !connects[i].ShouldAbort)
                                continue;

                            c = connects[i];
                            connects.RemoveAt(i);
                            break;
                        }

                        if (s == null && r == null && beginConnect == null)
                            handle.Reset();
                    }
                    
                    if(beginConnect != null)
                        DoConnect(beginConnect);

                    if (r != null)
                        DoReceive(r);

                    if (s != null)
                        DoSend(s);

                    if (c != null)
                        CompleteConnect(c);

                    handle.WaitOne(1000, false);
                }
            });
            t.IsBackground = true;
            t.Name = "NetworkIO";
            t.Start();
        }

        #region Asynchronous Sends

        private static void DoSend(AsyncIO s)
        {
            try
            {
                s.Connection.BeginSend(s.Buffer, s.Offset, s.Count, EndSend, s);
            }
            catch
            {
                s.Callback(false, 0, s.State);
            }
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

            io.Callback(succeeded, count, io.State);
        }

        internal static void EnqueueSend(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueSend(connection, buffer.Array, buffer.Offset + offset, count, callback, state);
        }

        internal static void EnqueueSend(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            lock (locker)
            {
                sends.Add(new AsyncIO(connection, buffer, offset, count, callback, state));
                handle.Set();
            }
        }

        #endregion Asynchronous Sends


        #region Asynchronous Receives

        private static void DoReceive(AsyncIO io)
        {
            try
            {
                io.Connection.BeginReceive(io.Buffer, io.Offset, io.Count, EndReceive, io);
            }
            catch
            {
                io.Callback(false, 0, io.State);
            }
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

            io.Callback(succeeded, count, io.State);
        }

        internal static void EnqueueReceive(IConnection connection, ArraySegment<byte> buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            EnqueueReceive(connection, buffer.Array, buffer.Offset + offset, count, callback, state);
        }

        internal static void EnqueueReceive(IConnection connection, byte[] buffer, int offset, int count, AsyncTransfer callback, object state)
        {
            lock (locker)
            {
                receives.Add(new AsyncIO(connection, buffer, offset, count, callback, state));
                handle.Set();
            }
        }

        #endregion Asynchronous Receives


        #region Asynchronous Connections

        private static void CompleteConnect(AsyncConnectState connect)
        {
            bool succeeded = true;
            try
            {
                if (connect.ShouldAbort)
                {
                    connect.Connection.Dispose();
                    succeeded = false;
                }
                else
                {
                    connect.Connection.EndConnect(connect.Result);
                }
            }
            catch
            {
                succeeded = false;
            }

            connect.Callback(succeeded, connect);
            connect.Result.AsyncWaitHandle.Close();
        }

        private static void DoConnect(AsyncConnectState c)
        {
            try
            {
                c.StartTime = Environment.TickCount;
                c.Result = c.Connection.BeginConnect(null, c);
                lock (locker)
                    connects.Add(c);
            }
            catch (Exception)
            {
                c.Callback(false, c);
            }
        }

        internal static void EnqueueConnect(AsyncConnectState connect)
        {
            lock (locker)
            {
                pendingConnects.Add(connect);
                handle.Set();
            }
        }

        #endregion Asynchronous Connections
    }
}
