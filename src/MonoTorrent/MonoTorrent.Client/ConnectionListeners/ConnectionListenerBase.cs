//
// IConnectionListener.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2007 Alan McGovern
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
using MonoTorrent.Client.Connections;
using MonoTorrent.Common;
namespace MonoTorrent.Client
{
    public enum ListenerStatus
    {
        Listening,
        PortNotFree,
        NotListening
    }

    public abstract class ConnectionListenerBase
    {
        public event EventHandler<NewConnectionEventArgs> ConnectionReceived;
        public event EventHandler<EventArgs> StateChanged;

        private ListenerStatus state;

        public abstract int ListenPort { get; }
        public ListenerStatus State
        {
            get { return state; }
        }

        public abstract void ChangePort(int port);
        public abstract void Start();
        public abstract void Stop();

        protected virtual void RaiseConnectionReceived(Peer peer, IConnection connection, TorrentManager manager)
        {
            Toolbox.RaiseAsyncEvent<NewConnectionEventArgs>(ConnectionReceived, this, new NewConnectionEventArgs(peer, connection, manager));
        }
        protected virtual void RaiseStateChanged(ListenerStatus status)
        {
            this.state = status;
            Toolbox.RaiseAsyncEvent<EventArgs>(StateChanged, this, EventArgs.Empty);
        }
    }
}
