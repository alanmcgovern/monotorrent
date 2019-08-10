//
// PeerConnectionFailedEventArgs.cs
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

namespace MonoTorrent.Client
{
    public class PeerConnectionFailedEventArgs : TorrentEventArgs
    {
        private Peer peer;
        private Direction connectionDirection;
        private String message;

        /// <summary>
        /// Peer from which this event happened
        /// </summary>
        public Peer Peer
        {
            get { return this.peer; }
        }

        /// <summary>
        /// Direction of event (if our connection failed to them or their connection failed to us)
        /// </summary>
        public Direction ConnectionDirection
        {
            get { return this.connectionDirection; }
        }

        /// <summary>
        /// Any message that might be associated with this event
        /// </summary>
        public String Message
        {
            get { return message; }
        }


        /// <summary>
        /// Create new instance of PeerConnectionFailedEventArgs for peer from given torrent.
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="peer"></param>
        /// <param name="direction">Which direction the connection attempt was</param>
        /// <param name="message">Message associated with the failure</param>
        public PeerConnectionFailedEventArgs(TorrentManager manager, Peer peer, Direction direction, String message)
            : base(manager)
        {
            this.peer = peer;
            this.connectionDirection = direction;
            this.message = message;
        }
    }
}
