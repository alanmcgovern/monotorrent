//
// HTTPConnection.cs
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
using MonoTorrent.Client.Encryption;
using System.Net.Sockets;

namespace MonoTorrent.Client.PeerConnections
{
    internal class HTTPConnection : PeerConnectionBase
    {
        public HTTPConnection(string location, int bitfieldLength, IEncryptorInternal encryptor)
            : base(bitfieldLength, encryptor)
        {
            throw new NotImplementedException();
        }
        internal override void BeginConnect(AsyncCallback peerEndCreateConnection, PeerIdInternal id)
        {
            throw new NotImplementedException();
        }

        internal override void BeginReceive(ArraySegment<byte> buffer, int offset, int count, System.Net.Sockets.SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id, out SocketError errorCode)
        {
            throw new NotImplementedException();
        }

        internal override void BeginSend(ArraySegment<byte> buffer, int offset, int count, System.Net.Sockets.SocketFlags socketFlags, AsyncCallback asyncCallback, PeerIdInternal id, out SocketError errorCode)
        {
            throw new NotImplementedException();
        }

        internal override void EndConnect(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        internal override int EndReceive(IAsyncResult result, out SocketError errorCode)
        {
            throw new NotImplementedException();
        }

        internal override int EndSend(IAsyncResult result, out SocketError errorCode)
        {
            throw new NotImplementedException();
        }

        internal override void Dispose()
        {
            throw new NotImplementedException();
        }

        internal override byte[] AddressBytes
        {
            get { return null; }
        }
    }
}
