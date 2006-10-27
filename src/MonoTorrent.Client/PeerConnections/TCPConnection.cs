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
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace MonoTorrent.Client.Encryption
{
    internal class TCPConnection : PeerConnectionBase
    {
        #region Member Variables
        public IPEndPoint PeerEndPoint
        {
            get { return this.peerEndPoint; }
        }
        private IPEndPoint peerEndPoint;


        private Socket peerSocket;
        #endregion


        #region Constructors
        public TCPConnection(string location, int bitfieldLength, IEncryptor encryptor)
            : base(bitfieldLength, encryptor)
        {
            string[] s = location.Split(':');
            if (s.Length != 2)
                throw new ArgumentException("Location should be in the form ipaddress:port", "location");

            this.peerEndPoint = new IPEndPoint(IPAddress.Parse(s[0]), Convert.ToInt32(s[1]));
            this.peerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public TCPConnection(IPEndPoint endPoint, int bitfieldLength, IEncryptor encryptor)
            : base(bitfieldLength, encryptor)
        {
            this.peerEndPoint = endPoint;
            this.peerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public TCPConnection(Socket socket, int bitfieldLength, IEncryptor encryptor)
            : base(bitfieldLength, encryptor)
        {
            this.peerEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            this.peerSocket = socket;
        }
        #endregion


        #region Async Methods
        internal override void BeginConnect(AsyncCallback peerEndCreateConnection, PeerConnectionID id)
        {
            this.peerSocket.BeginConnect(this.peerEndPoint, peerEndCreateConnection, id);
        }

        internal override void BeginReceive(byte[] buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerConnectionID id, out SocketError errorCode)
        {
#warning Until mono supports the 'out errorcode' overload, we continue as before
            errorCode = SocketError.Success;
            this.peerSocket.BeginReceive(buffer, offset, count, socketFlags, asyncCallback, id);
            //this.peerSocket.BeginReceive(buffer, offset, count, socketFlags, out errorCode, asyncCallback, id);
        }

        internal override void BeginSend(byte[] buffer, int offset, int count, SocketFlags socketFlags, AsyncCallback asyncCallback, PeerConnectionID id, out SocketError errorCode)
        {
#warning Until mono supports the 'out errorcode' overload, we continue as before
            errorCode = SocketError.Success;
            this.peerSocket.BeginSend(buffer, offset, count, socketFlags, asyncCallback, id);
            //this.peerSocket.BeginSend(buffer, offset, count, socketFlags, out errorCode, asyncCallback, id);
        }

        internal override void EndConnect(IAsyncResult result)
        {
            this.peerSocket.EndConnect(result);
        }

        internal override int EndSend(IAsyncResult result, out SocketError errorCode)
        {
#warning Until mono supports the 'out errorcode' overload, we continue as before
            errorCode = SocketError.Success;
            return this.peerSocket.EndSend(result);
            //return this.peerSocket.EndSend(result, out errorCode);
        }

        internal override int EndReceive(IAsyncResult result, out SocketError errorCode)
        {
#warning Until mono supports the 'out errorcode' overload, we continue as before
            errorCode = SocketError.Success;
            return this.peerSocket.EndReceive(result);
            //return this.peerSocket.EndReceive(result, out errorCode);
        }

        public override void Dispose()
        {
            this.peerSocket.Close();
        }
        #endregion
    }
}
