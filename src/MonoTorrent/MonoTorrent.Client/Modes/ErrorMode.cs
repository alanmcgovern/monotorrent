//
// ErrorMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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

namespace MonoTorrent.Client
{
    // In the error mode, we just disable all connections
    // Usually we enter this because the HD is full
    public enum Reason
    {
        ReadFailure,
        WriteFailure
    }
    public class Error
    {
        Exception exception;
        Reason reason;
        public Error(Reason reason, Exception exception)
        {
            this.reason = reason;
            this.exception = exception;
        }
        public Exception Exception
        {
            get { return exception; }
        }
        public Reason Reason
        {
            get { return reason; }
        }
    }

    class ErrorMode : Mode
    {
        public override TorrentState State
        {
            get { return TorrentState.Error; }
        }

        public ErrorMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
            CloseConnections();
        }

        public override void Tick(int counter)
        {
            Manager.Monitor.Reset();
            CloseConnections();
        }

        void CloseConnections()
        {
            foreach (PeerId id in Manager.Peers.ConnectedPeers)
                Manager.Engine.ConnectionManager.CleanupSocket (id);
        }

        public override void HandlePeerConnected (PeerId id)
        {
            Manager.Engine.ConnectionManager.CleanupSocket (id);
        }
    }
}
