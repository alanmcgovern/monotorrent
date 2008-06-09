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
    internal class AsyncConnect
    {
        public AsyncConnect(TorrentManager manager, Peer peer, IConnection connection, AsyncCallback callback)
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

        public AsyncCallback Callback;
        public IConnection Connection;
        public TorrentManager Manager;
        public Peer Peer;
        public IAsyncResult Result;
        public int StartTime;
    }

    internal static class NetworkIO
    {
        private struct AsyncIO
        {
            public AsyncIO(IAsyncResult result, AsyncCallback callback)
            {
                Result = result;
                Callback = callback;
            }

            public IAsyncResult Result;
            public AsyncCallback Callback;
        }
        private static List<AsyncConnect> connects;
        private static List<AsyncIO> receives;
        private static List<AsyncIO> sends;

        public static int HalfOpens
        {
            get { lock (connects) return connects.Count; }
        }

        static NetworkIO()
        {
            connects = new List<AsyncConnect>();
            receives = new List<AsyncIO>();
            sends = new List<AsyncIO>();

            Thread t = new Thread((ThreadStart)delegate
            {
                while (true)
                {
                    AsyncConnect c = null;
                    AsyncIO? r = null;
                    AsyncIO? s = null;

                    lock (receives)
                    {
                        for (int i = 0; i < receives.Count; i++)
                        {
                            if (!receives[i].Result.IsCompleted)
                                continue;
                            r = receives[i];
                            receives.RemoveAt(i);
                            break;
                        }
                    }

                    lock (sends)
                    {
                        for (int i = 0; i < sends.Count; i++)
                        {
                            if (!sends[i].Result.IsCompleted)
                                continue;
                            s = sends[i];
                            sends.RemoveAt(i);
                            break;
                        }
                    }
                    lock (connects)
                    {
                        for (int i = 0; i < connects.Count; i++)
                        {
                            if (!connects[i].Result.IsCompleted && !connects[i].ShouldAbort)
                                continue;
                            c = connects[i];
                            connects.RemoveAt(i);
                            break;
                        }
                    }

                    if (r.HasValue)
                    {
                        r.Value.Callback(r.Value.Result);
                        r.Value.Result.AsyncWaitHandle.Close();
                    }
                    if (s.HasValue)
                    {
                        s.Value.Callback(s.Value.Result);
                        s.Value.Result.AsyncWaitHandle.Close();
                    }
                    if (c != null)
                        CompleteConnect(c);

                    System.Threading.Thread.Sleep(1);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private static void CompleteConnect(AsyncConnect connect)
        {
            if (connect.ShouldAbort)
                connect.Connection.Dispose();

            connect.Callback(connect.Result);
            connect.Result.AsyncWaitHandle.Close();
        }


        internal static void EnqueueSend(ArraySegment<byte> sendBuffer, int bytesSent, int count, AsyncCallback callback, PeerIdInternal id)
        {
            IAsyncResult result = id.Connection.BeginSend(sendBuffer, bytesSent, count, SocketFlags.None, null, id);
            lock (sends)
                sends.Add(new AsyncIO(result, callback));
        }

        internal static void EnqueueReceive(ArraySegment<byte> receiveBuffer, int bytesReceived, int count, AsyncCallback callback, PeerIdInternal id)
        {
            IAsyncResult result = id.Connection.BeginReceive(receiveBuffer, bytesReceived, count, SocketFlags.None, null, id);
            lock (receives)
                receives.Add(new AsyncIO(result, callback));
        }

        internal static void EnqueueConnect(AsyncConnect connect)
        {
            connect.Result = connect.Connection.BeginConnect(null, connect);
            connect.StartTime = Environment.TickCount;
            
            lock (connects)
                connects.Add(connect);
        }
    }
}
