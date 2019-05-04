//
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
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client.Connections
{
    public class IPV4Connection : IConnection
    {
        SocketAsyncEventArgs receiveArgs;
        SocketAsyncEventArgs sendArgs;

        private bool isIncoming;
        private IPEndPoint endPoint;
        private Socket socket;
        private Uri uri;

        #region Member Variables

        public bool CanReconnect
        {
            get { return !isIncoming; }
        }

        public bool Connected
        {
            get { return socket.Connected; }
        }

        EndPoint IConnection.EndPoint
        {
            get { return endPoint; }
        }

        public IPEndPoint EndPoint
        {
            get { return this.endPoint; }
        }

        public bool IsIncoming
        {
            get { return isIncoming; }
        }

        public Uri Uri
        {
            get { return uri; }
        }

        #endregion


        #region Constructors

        public IPV4Connection(Uri uri)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), 
                   new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port),
                   false)
        {
            this.uri = uri;
        }

        public IPV4Connection(IPEndPoint endPoint)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), endPoint, false)
        {

        }

        public IPV4Connection(Socket socket, bool isIncoming)
            : this(socket, (IPEndPoint)socket.RemoteEndPoint, isIncoming)
        {

        }


        private IPV4Connection (Socket socket, IPEndPoint endpoint, bool isIncoming)
        {
            this.receiveArgs = new SocketAsyncEventArgs {
                RemoteEndPoint = endpoint
            };
            this.sendArgs = new SocketAsyncEventArgs {
                RemoteEndPoint = endpoint
            };
            receiveArgs.Completed += HandleOperationCompleted;
            sendArgs.Completed += HandleOperationCompleted;
            this.socket = socket;
            this.endPoint = endpoint;
            this.isIncoming = isIncoming;
        }

        static void HandleOperationCompleted (object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
                ((TaskCompletionSource<int>)e.UserToken).SetException(new SocketException((int)e.SocketError));
            else
                ((TaskCompletionSource<int>)e.UserToken).SetResult(e.BytesTransferred);

        }

        #endregion


        #region Async Methods

        public byte[] AddressBytes
        {
            get { return this.endPoint.Address.GetAddressBytes(); }
        }

        public Task ConnectAsync ()
        {
            var tcs = new TaskCompletionSource<int>();
            receiveArgs.UserToken = tcs;

            socket.ConnectAsync (receiveArgs);
            return tcs.Task;
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();
            receiveArgs.SetBuffer(buffer, offset, count);
            receiveArgs.UserToken = tcs;

            socket.ReceiveAsync(receiveArgs);
            return tcs.Task;
        }

        public Task<int> SendAsync(byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();
            sendArgs.SetBuffer(buffer, offset, count);
            sendArgs.UserToken = tcs;

            socket.SendAsync(sendArgs);
            return tcs.Task;
        }


        public void Dispose()
        {
            ((IDisposable)socket).Dispose();
        }


        #endregion
    }
}