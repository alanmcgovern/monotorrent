//
// KeepAliveMessage.cs
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

namespace MonoTorrent.Messages.Peer
{
    /// <summary>
    /// Represents a "KeepAlive" message
    /// </summary>
    public class KeepAliveMessage : PeerMessage
    {
        public static KeepAliveMessage Instance { get; } = new KeepAliveMessage ();

        static readonly byte[] payload = { 0, 0, 0, 0 };

        #region Constructors
        /// <summary>
        /// Creates a new KeepAliveMessage
        /// </summary>
        public KeepAliveMessage ()
        {
        }
        #endregion


        #region Methods
        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, payload);

            return written - buffer.Length;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            // No decoding needed
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength => (4);
        #endregion


        #region Overridden Methods
        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            return "KeepAliveMessage";
        }

        public override bool Equals (object obj)
        {
            return (obj is KeepAliveMessage);
        }


        public override int GetHashCode ()
        {
            return ToString ().GetHashCode ();
        }
        #endregion
    }
}