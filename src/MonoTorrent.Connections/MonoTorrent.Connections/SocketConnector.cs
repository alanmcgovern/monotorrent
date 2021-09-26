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
            e.RemoteEndPoint = null;
            e.UserToken = null;

            lock (cache)
                cache.Enqueue (e);

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

            if (!socket.ConnectAsync (args))
                tcs.SetResult (0);

            await tcs.Task;
            return socket;
        }
    }
}
