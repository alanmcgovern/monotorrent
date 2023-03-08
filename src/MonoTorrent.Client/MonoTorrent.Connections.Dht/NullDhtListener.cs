﻿//
// NullDhtListener.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2020 Alan McGovern
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
using System.Threading.Tasks;

namespace MonoTorrent.Connections.Dht
{
    class NullDhtListener : IDhtListener
    {
        public static IDhtListener IPv4 { get; } = new NullDhtListener (new IPEndPoint (IPAddress.Any, 0));
        public static IDhtListener IPv6 { get; } = new NullDhtListener (new IPEndPoint (IPAddress.IPv6Any, 0));

#pragma warning disable 0067
        public event Action<byte[], IPEndPoint>? MessageReceived;
        public event EventHandler<EventArgs>? StatusChanged;
#pragma warning restore 0067

        public IPEndPoint? LocalEndPoint { get; }
        public IPEndPoint PreferredLocalEndPoint { get; }
        public ListenerStatus Status { get; } = ListenerStatus.NotListening;

        public NullDhtListener (IPEndPoint endPoint)
            => (PreferredLocalEndPoint) = endPoint;

        public Task SendAsync (byte[] buffer, IPEndPoint endpoint)
        {
            return Task.CompletedTask;
        }

        public void Start ()
        {
        }

        public void Stop ()
        {
        }
    }
}
