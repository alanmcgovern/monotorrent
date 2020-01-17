//
// EncryptionTypes.cs
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

namespace MonoTorrent.Client
{
    [Flags]
    public enum EncryptionTypes
    {
        /// <summary>
        /// <see cref="Peer.AllowedEncryption"/> is initially set to <see cref="All"/>. If a
        /// connection cannot be established using a given method that method is removed from
        /// the list. If every method fails then the peer will be set to <see cref="None"/>
        /// and no further connections will be attempted. By setting <see cref="None"/> then
        /// encrypted and unencrypted connections are both disallowed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Nothing is encrypted. This is the fastest but allows deep packet inspection to detect
        /// the bittorrent handshake. If connections are being closed before the handshake completes,
        /// or very soon after it completes, then it's possible that the ISP is closing them, and so
        /// RC4 based methods may prevent that from happening.
        /// </summary>
        PlainText = 1 << 0,

        /// <summary>
        /// Encryption is applied to the initial handshaking process only. Once the connection has
        /// been established all further data is sent in plain text. This is the second fastest
        /// and should prevent deep packet inspection from detecting the bittorrent handshake.
        /// </summary>
        RC4Header = 1 << 1,

        /// <summary>
        /// Encryption is applied to the initial handshake and to all subsequent data transfers.
        /// </summary>
        RC4Full = 1 << 2,

        /// <summary>
        /// Both encrypted and unencrypted connections are allowed.
        /// </summary>
        All = PlainText | RC4Full | RC4Header
    }
}
