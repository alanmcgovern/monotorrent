//
// NullConnection.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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

using MonoTorrent.Connections.Peer;

using ReusableTasks;

namespace MonoTorrent.Client
{
    public class NullConnection : IPeerConnection
    {
        public static NullConnection Incoming = new NullConnection (true);
        public static NullConnection Outgoing = new NullConnection (false);

        public ReadOnlyMemory<byte> AddressBytes { get; } = new byte[] { 1, 2, 3, 4 };

        public bool CanReconnect => false;

        public bool Disposed => false;

        public IPEndPoint EndPoint => new IPEndPoint (IPAddress.Parse (Uri.Host), Uri.Port);

        public bool IsIncoming { get; }

        public Uri Uri => new Uri ($"ipv4://1.2.3.4:5678");

        NullConnection (bool isIncoming)
        {
            IsIncoming = isIncoming;
        }

        public ReusableTask ConnectAsync ()
        {
            return ReusableTask.CompletedTask;
        }

        public void Dispose ()
        {
        }

        public ReusableTask<int> ReceiveAsync (Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }

        public ReusableTask<int> SendAsync (Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }
    }
}
