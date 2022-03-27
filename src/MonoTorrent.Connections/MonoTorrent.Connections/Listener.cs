//
// Listener.cs
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
using System.Threading;

namespace MonoTorrent.Connections
{
    public abstract class Listener : IListener
    {
        public event EventHandler<EventArgs>? StatusChanged;

        CancellationTokenSource Cancellation { get; set; }
        public ListenerStatus Status { get; private set; }

        protected Listener ()
        {
            Cancellation = new CancellationTokenSource ();
            Status = ListenerStatus.NotListening;
        }

        void RaiseStatusChanged (ListenerStatus status)
        {
            Status = status;
            StatusChanged?.Invoke (this, EventArgs.Empty);
        }

        public void Start ()
        {
            if (Status == ListenerStatus.Listening)
                return;

            Cancellation.Cancel ();
            Cancellation = new CancellationTokenSource ();
            Cancellation.Token.Register (() => RaiseStatusChanged (ListenerStatus.NotListening));

            try {
                Start (Cancellation.Token);
                RaiseStatusChanged (ListenerStatus.Listening);
            } catch {
                RaiseStatusChanged (ListenerStatus.PortNotFree);
            }

        }

        protected abstract void Start (CancellationToken token);

        public void Stop ()
        {
            Cancellation.Cancel ();
        }
    }
}
