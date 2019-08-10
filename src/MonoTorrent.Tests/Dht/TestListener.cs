//
// TestListener.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht
{
    class TestListener : IDhtListener
    {
        public event Action<Message, IPEndPoint> MessageSent;
        public event MessageReceived MessageReceived;
        public event EventHandler<EventArgs> StatusChanged;

        public IPEndPoint Endpoint { get; private set; } = new IPEndPoint(IPAddress.Loopback, 0);
        public ListenerStatus Status { get; private set; }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
            => MessageReceived?.Invoke (message.Encode (), endpoint);

        public void ChangeEndpoint (IPEndPoint endpoint)
            => Endpoint = endpoint;

        public Task SendAsync(byte[] buffer, IPEndPoint endpoint)
        {
            Message message;
            MessageFactory.TryDecodeMessage (BEncodedValue.Decode<BEncodedDictionary> (buffer), out message);
            MessageSent?.Invoke (message, endpoint);
            return Task.CompletedTask;
        }

        public void Start ()
            => SetStatus (ListenerStatus.Listening);

        public void Stop ()
            => SetStatus (ListenerStatus.NotListening);

        void SetStatus (ListenerStatus status)
        {
            Status = status;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }
    }
}
