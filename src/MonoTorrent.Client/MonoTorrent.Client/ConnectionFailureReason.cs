//
// ConnectionFailureReason.cs
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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Lists potential reasons why an outgoing connection could not be established.
    /// </summary>
    public enum ConnectionFailureReason
    {
        /// <summary>
        /// The remote peer did not accept the connection. This might mean the client has gone
        /// offline or the client is behind a NAT/Firewall and cannot accept incoming connections.
        /// </summary>
        Unreachable,

        /// <summary>
        /// After accepting the connection, a compatible connection encryption method could not
        /// be selected. Alternatively the remote peer could have reached it's open connection
        /// limit and simply closed the connection, or it could mean the peer did not support
        /// encrypted connections.
        /// </summary>
        EncryptionNegiotiationFailed,

        /// <summary>
        /// A compatible connection encryption method was selected, but the peer closed the
        /// connection after the normal handshake was sent. This could mean the remote peer
        /// reached it's open connection limit and simply closed the connection, or it could
        /// mean the peer did not actually support the chosen encryption. This can happen
        /// if we choose to create an unencrypted outgoing connection and the peer only
        /// supports encrypted connections.
        /// </summary>
        HandshakeFailed,

        /// <summary>
        /// There is no clear reason why the connection attempt failed.
        /// </summary>
        Unknown,
    }
}
