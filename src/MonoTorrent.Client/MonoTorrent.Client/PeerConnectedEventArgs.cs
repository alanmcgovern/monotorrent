//
// PeerConnectionEventArgs.cs
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

using MonoTorrent.Connections.Peer;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Used by the <see cref="TorrentManager.PeerConnected"/> event
    /// </summary>
    public sealed class PeerConnectedEventArgs : TorrentEventArgs
    {
        /// <summary>
        /// <see cref="Direction.Incoming"/> if the connection was received by the <see cref="IPeerConnectionListener"/> associated
        /// with the active <see cref="ClientEngine"/>, otherwise <see cref="Direction.Outgoing"/> if the
        /// connection was created by the active <see cref="TorrentManager"/>
        /// </summary>
        public Direction Direction => Peer.Connection.IsIncoming ? Direction.Incoming : Direction.Outgoing;

        /// <summary>
        /// The object which will track the current session with this peer.
        /// </summary>
        public PeerId Peer { get; }

        internal PeerConnectedEventArgs (TorrentManager manager, PeerId id)
            : base (manager)
        {
            Peer = id;
        }
    }
}
