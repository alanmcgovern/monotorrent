//
// System.String.cs
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

namespace MonoTorrent.Common
{
    public abstract class Peer
    {
        public IPEndPoint PeerEndpoint
        {
            get { return this.peerEndpoint; }
        }
        private IPEndPoint peerEndpoint;

        public string PeerId
        {
            get { return peerId; }
            set { peerId = value; }
        }
        private string peerId;

        public int FailedConnectionAttempts
        {
            get { return this.failedConnectionAttempts; }
            set { this.failedConnectionAttempts = value; }
        }
        private int failedConnectionAttempts;

        public int LastMessageSent
        {
            get { return this.lastMessageSent; }
            set { this.lastMessageSent = value; }
        }
        private int lastMessageSent;

        public int LastMessageRecieved
        {
            get { return this.lastMessageRecieved; }
            set { this.lastMessageRecieved = value; }
        }
        private int lastMessageRecieved;

        public Peer(IPEndPoint peerEndpoint, string peerId)
        {
            this.peerEndpoint = peerEndpoint;
            this.peerId = peerId;
        }
    }
}