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


namespace MonoTorrent.Client
{
    /// <summary>
    /// Used by the <see cref="TorrentManager.ConnectionAttemptFailed"/> event
    /// </summary>
    public sealed class ConnectionAttemptFailedEventArgs : TorrentEventArgs
    {
        /// <summary>
        /// This is a guess about why the engine failed to establish an outgoing connection.
        /// </summary>
        public ConnectionFailureReason Reason { get; }

        /// <summary>
        /// The peer which could not be connected to.
        /// </summary>
        public PeerInfo Peer { get; }

        internal ConnectionAttemptFailedEventArgs (PeerInfo peer, ConnectionFailureReason reason, TorrentManager manager)
            : base (manager)
        {
            Peer = peer;
            Reason = reason;
        }
    }
}
