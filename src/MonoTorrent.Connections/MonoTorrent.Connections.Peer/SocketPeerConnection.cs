﻿//
// SocketConnection.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    public sealed class SocketPeerConnection : IPeerConnection
    {
        static readonly EventHandler<SocketAsyncEventArgs> Handler = HandleOperationCompleted;

        /// <summary>
        /// Where possible we will use a SocketAsyncEventArgs object which has already had
        /// 'SetBuffer(byte[],int,int)' invoked on it for the given byte[]. Reusing these is
        /// much more efficient than constantly calling SetBuffer on a different 'SocketAsyncEventArgs'
        /// object.
        /// </summary>
        /// <param name="buffer">The buffer we wish to get the reusuable 'SocketAsyncEventArgs' for</param>
        /// <returns></returns>
        static SocketAsyncEventArgs GetSocketAsyncEventArgs (SocketMemory buffer)
        {

#if NETSTANDARD2_0
            if (buffer.SocketArgs.Buffer == null)
                buffer.SocketArgs.Completed += Handler;

            if (!MemoryMarshal.TryGetArray (buffer.Memory, out ArraySegment<byte> segment))
                throw new ArgumentException ("Could not retrieve the underlying buffer");

            if (buffer.SocketArgs.Buffer == null)
                buffer.SocketArgs.SetBuffer (segment.Array, segment.Offset, segment.Count);
            else
                buffer.SocketArgs.SetBuffer (segment.Offset, segment.Count);
#else
            if (buffer.SocketArgs.MemoryBuffer.IsEmpty)
                buffer.SocketArgs.Completed += Handler;
            buffer.SocketArgs.SetBuffer (buffer.Memory);
#endif
            return buffer.SocketArgs;
        }

#region Member Variables

        public byte[] AddressBytes => EndPoint.Address.GetAddressBytes ();

        public bool CanReconnect => !IsIncoming;

        CancellationTokenSource ConnectCancellation { get; }

        ISocketConnector Connector { get; }

        public bool Disposed { get; private set; }

        EndPoint IPeerConnection.EndPoint => EndPoint;

        public IPEndPoint EndPoint { get; }

        public bool IsIncoming { get; }

        ReusableTaskCompletionSource<int> ReceiveTcs { get; }

        ReusableTaskCompletionSource<int> SendTcs { get; }

        Socket Socket { get; set; }

        public Uri Uri { get; }

#endregion


#region Constructors

        public SocketPeerConnection (Socket socket, bool isIncoming)
            : this (null, null, socket, isIncoming)
        {

        }

        public SocketPeerConnection (Uri uri, ISocketConnector connector)
            : this (uri, connector, null, false)
        {

        }

        SocketPeerConnection (Uri uri, ISocketConnector connector, Socket socket, bool isIncoming)
        {
            if (uri == null) {
                var endpoint = (IPEndPoint) socket.RemoteEndPoint;
                uri = new Uri ($"{(socket.AddressFamily == AddressFamily.InterNetwork ? "ipv4" : "ipv6") }://{endpoint.Address}{':'}{endpoint.Port}");
            }

            ConnectCancellation = new CancellationTokenSource ();
            Connector = connector;
            EndPoint = new IPEndPoint (IPAddress.Parse (uri.Host), uri.Port);
            IsIncoming = isIncoming;
            Socket = socket;
            Uri = uri;

            ReceiveTcs = new ReusableTaskCompletionSource<int> ();
            SendTcs = new ReusableTaskCompletionSource<int> ();
        }

        static void HandleOperationCompleted (object sender, SocketAsyncEventArgs e)
        {
            // Don't retain the TCS forever. Note we do not want to null out the byte[] buffer
            // as we *do* want to retain that so that we can avoid the expensive SetBuffer calls.
            var tcs = (ReusableTaskCompletionSource<int>) e.UserToken;
            SocketError error = e.SocketError;
            int transferred = e.BytesTransferred;
            e.RemoteEndPoint = null;
            e.UserToken = null;

            if (error != SocketError.Success)
                tcs.SetException (new SocketException ((int) error));
            else
                tcs.SetResult (transferred);
        }

#endregion


#region Async Methods

        public async ReusableTask ConnectAsync ()
        {
            Socket = await Connector.ConnectAsync (Uri, ConnectCancellation.Token);
            if (Disposed) {
                Socket.Dispose ();
                throw new SocketException ((int) SocketError.Shutdown);
            }
        }

        public ReusableTask<int> ReceiveAsync (SocketMemory buffer)
        {
            SocketAsyncEventArgs args = GetSocketAsyncEventArgs (buffer);
            args.UserToken = ReceiveTcs;

            AsyncFlowControl? control = null;
            if (!ExecutionContext.IsFlowSuppressed ())
                control = ExecutionContext.SuppressFlow ();

            try {
                if (!Socket.ReceiveAsync (args))
                    ReceiveTcs.SetResult (args.BytesTransferred);
            } catch (ObjectDisposedException) {
                ReceiveTcs.SetResult (0);
            } finally {
                control?.Undo ();
            }

            return ReceiveTcs.Task;
        }

        public ReusableTask<int> SendAsync (SocketMemory buffer)
        {
            SocketAsyncEventArgs args = GetSocketAsyncEventArgs (buffer);
            args.UserToken = SendTcs;

            AsyncFlowControl? control = null;
            if (!ExecutionContext.IsFlowSuppressed ())
                control = ExecutionContext.SuppressFlow ();

            try {
                if (!Socket.SendAsync (args))
                    SendTcs.SetResult (buffer.Length);
            } catch (ObjectDisposedException) {
                SendTcs.SetResult (0);
            } finally {
                control?.Undo ();
            }

            return SendTcs.Task;
        }

        public void Dispose ()
        {
            Disposed = true;
            ConnectCancellation.Cancel ();
            Socket?.Dispose ();
        }

#endregion
    }
}
