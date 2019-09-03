﻿//
// ManualLocalPeerListener.cs
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
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    class ManualLocalPeerListener : ILocalPeerDiscovery
    {
        public ListenerStatus Status { get; private set; }

        public IPEndPoint EndPoint { get; set; }

        public event EventHandler<LocalPeerFoundEventArgs> PeerFound;
        public event EventHandler<EventArgs> StatusChanged;

        public Task Announce (InfoHash infoHash)
            => Task.CompletedTask;

        public void RaisePeerFound (InfoHash infoHash, Uri uri)
            => PeerFound?.Invoke (this, new LocalPeerFoundEventArgs (infoHash, uri));

        public void Start ()
        {
            Status = ListenerStatus.Listening;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }

        public void Stop ()
        {
            Status = ListenerStatus.NotListening;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }
    }
}
