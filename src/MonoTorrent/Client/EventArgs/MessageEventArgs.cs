//
// MessageEventArgs.cs
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
using MonoTorrent.Common;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides the data needed to handle a PeerMessage event
    /// </summary>
    public class PeerMessageEventArgs : EventArgs
    {
        #region Member Variables

        /// <summary>
        /// The Peer message that was just sent/Received
        /// </summary>
        public IPeerMessage Message
        {
            get { return this.message; }
        }
        private IPeerMessage message;

        /// <summary>
        /// The direction of the message (outgoing/incoming)
        /// </summary>
        public Direction Direction
        {
            get { return this.direction; }
        }
        private Direction direction;

        public PeerConnectionID ID
        {
            get { return this.id; }
        }
        private PeerConnectionID id;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new PeerMessageEventArgs
        /// </summary>
        /// <param name="message">The peer message involved</param>
        /// <param name="direction">The direction of the message</param>
        public PeerMessageEventArgs(IPeerMessage message, Direction direction, PeerConnectionID id)
        {
            this.direction = direction;
            this.id = id;
            this.message = message;
        }

        #endregion
    }
}
