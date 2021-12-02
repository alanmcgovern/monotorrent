//
// SocketConnector.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Net;
using System.Net.Sockets;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Connections
{
    public class SocketConnector : ISocketConnector
    {
        static readonly Queue<SocketAsyncEventArgs> cache = new Queue<SocketAsyncEventArgs> ();
        static readonly EventHandler<SocketAsyncEventArgs> Handler = HandleOperationCompleted;
        static readonly Action<object> SocketDisposer = (state) => ((Socket) state).Dispose ();

        static SocketAsyncEventArgs GetSocketAsyncEventArgs ()
        {
            SocketAsyncEventArgs args;
            lock (cache) {
                if (cache.Count == 0) {
                    args = new SocketAsyncEventArgs ();
                    args.Completed += Handler;
                } else {
                    args = cache.Dequeue ();
                }
            }
            return args;
        }

        static void HandleOperationCompleted (object sender, SocketAsyncEventArgs e)
        {
            // Don't retain the TCS forever. Note we do not want to null out the byte[] buffer
            // as we *do* want to retain that so that we can avoid the expensive SetBuffer calls.
            var tcs = (ReusableTaskCompletionSource<int>) e.UserToken;
            SocketError error = e.SocketError;

            if (error != SocketError.Success)
                tcs.SetException (new SocketException ((int) error));
            else
                tcs.SetResult (0);
        }


        public async ReusableTask<Socket> ConnectAsync (Uri uri, CancellationToken token)
        {
            var socket = new Socket ((uri.Scheme == "ipv4") ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            var endPoint = new IPEndPoint (IPAddress.Parse (uri.Host), uri.Port);

            using var registration = token.Register (SocketDisposer, socket);
            var tcs = new ReusableTaskCompletionSource<int> ();
            SocketAsyncEventArgs args = GetSocketAsyncEventArgs ();
            args.RemoteEndPoint = endPoint;
            args.UserToken = tcs;

            try {
                if (!socket.ConnectAsync (args))
                    tcs.SetResult (0);

                await tcs.Task;
            } catch {
                socket.Dispose ();
                throw;
            } finally {
                args.RemoteEndPoint = null;
                args.UserToken = null;
                lock (cache)
                    cache.Enqueue (args);
            }
            return socket;
        }
    }
}
