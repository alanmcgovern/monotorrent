//
// DhtListener.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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

using ReusableTasks;

namespace MonoTorrent.Connections.Dht
{
    public class DhtListener : IDhtListener
    {
        readonly UdpListener listener;

        public event Action<ReadOnlyMemory<byte>, CompactEndPoint>? MessageReceived;

        public event EventHandler<EventArgs>? StatusChanged {
            add => listener.StatusChanged += value;
            remove => listener.StatusChanged -= value;
        }

        public IPEndPoint? LocalEndPoint => listener.LocalEndPoint;

        public ListenerStatus Status => listener.Status;

        public DhtListener (UdpListener listener)
        {
            this.listener = listener ?? throw new ArgumentNullException (nameof (listener));
            listener.MessageReceived += ListenerMessageReceived;
        }

        public ReusableTask SendAsync (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
            => listener.SendAsync (buffer, endpoint);

        public void Start ()
            => listener.Start ();

        public void Stop ()
            => listener.Stop ();

        void ListenerMessageReceived (ReadOnlyMemory<byte> buffer, CompactEndPoint endpoint)
        {
            if (!buffer.IsEmpty && buffer.Span[0] == (byte) 'd')
                MessageReceived?.Invoke (buffer, endpoint);
        }
    }
}
