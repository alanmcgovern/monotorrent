﻿//
// TCPConnection.cs
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
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace MonoTorrent.Client.Connections
{
    public class SocketConnection : IConnection
    {
        #region Member Variables

        public byte[] AddressBytes => EndPoint.Address.GetAddressBytes();

        public bool CanReconnect => !IsIncoming;

        public bool Connected => SK.Connected;

        EndPoint IConnection.EndPoint => EndPoint;

        public IPEndPoint EndPoint { get; private set; }

        public bool IsIncoming { get; }

        protected SocketAsyncEventArgs ReceiveArgs { get; private set; }

        protected SocketAsyncEventArgs SendArgs { get; private set; }

        protected Socket SK { get; private set; }

        public Uri Uri { get; protected set; }

		#endregion


		#region Constructors

		protected SocketConnection(Uri uri)
            : this (new Socket((uri.Scheme == "ipv4") ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp),
                  new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port), false)

        {
            Uri = uri;
        }

        protected SocketConnection(Socket socket, bool isIncoming)
            : this(socket, (IPEndPoint)socket.RemoteEndPoint, isIncoming)
        {

        }

        SocketConnection (Socket socket, IPEndPoint endpoint, bool isIncoming)
        {
            ReceiveArgs = new SocketAsyncEventArgs {
                RemoteEndPoint = endpoint
            };
            SendArgs = new SocketAsyncEventArgs {
                RemoteEndPoint = endpoint
            };
            ReceiveArgs.Completed += HandleOperationCompleted;
            SendArgs.Completed += HandleOperationCompleted;
            SK = socket;
            EndPoint = endpoint;
            IsIncoming = isIncoming;
        }

        //去掉static函数，否则导致大量的ReceiveArgs以及SendArgs不能释放--因为Dispose函数里也不能释放
        static void HandleOperationCompleted (object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                ((TaskCompletionSource<int>)e.UserToken).SetException(new SocketException((int)e.SocketError));
            else
                ((TaskCompletionSource<int>)e.UserToken).SetResult(e.BytesTransferred);
        }

        #endregion


        #region Async Methods

        public Task ConnectAsync ()
        {
            var tcs = new TaskCompletionSource<int>();
            ReceiveArgs.UserToken = tcs;

            if (!SK.ConnectAsync(ReceiveArgs))
                return Task.FromResult(true);
            return tcs.Task;
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();
            ReceiveArgs.SetBuffer(buffer, offset, count);
            ReceiveArgs.UserToken = tcs;

            if (!SK.ReceiveAsync(ReceiveArgs))
                return Task.FromResult(ReceiveArgs.BytesTransferred);
            return tcs.Task;
        }

        public Task<int> SendAsync(byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();
            SendArgs.SetBuffer(buffer, offset, count);
            SendArgs.UserToken = tcs;

            if (!SK.SendAsync(SendArgs))
                return Task.FromResult(SendArgs.BytesTransferred);
            return tcs.Task;
        }

        public void Dispose()
        {
            try
            {
                if (null != ReceiveArgs)
                {
                    ReceiveArgs.Completed -= HandleOperationCompleted;
                    SendArgs.Completed -= HandleOperationCompleted;

                    ReceiveArgs.RemoteEndPoint = null;
                    SendArgs.RemoteEndPoint = null;

                    ReceiveArgs.Dispose();
                    SendArgs.Dispose();

                    ReceiveArgs = null;
                    SendArgs = null;

                    //EndPoint = null;//reserved for Logging
                    Uri = null;

                    SK.Dispose();
                    SK = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(null, " SocketConnection dispose error " + EndPoint + " " + " IsIncoming " + IsIncoming + " " + ex.Message);
            }
        }

        #endregion
    }
}