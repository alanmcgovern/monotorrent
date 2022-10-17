//
// PortMessage.cs
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
    /// Represents a "Port" message
    /// </summary>
    public class PortMessage : PeerMessage, IRentable
    {
        const int messageLength = 3;
        internal const byte MessageId = 9;

        #region Private Fields
        ushort port;
        #endregion


        #region Public Properties

        public override int ByteLength => (messageLength + 4);

        public int Port => port;

        #endregion


        #region Constructors
        public PortMessage ()
        {
        }

        public PortMessage (ushort port)
        {
            this.port = port;
        }

        #endregion


        #region Methods

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            port = ReadUShort (ref buffer);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length ;

            Write (ref buffer, messageLength);
            Write (ref buffer, MessageId);
            Write (ref buffer, (ushort)port);

            return written - buffer.Length;
        }

        public override bool Equals (object? obj)
        {
            return (!(obj is PortMessage msg)) ? false : (port == msg.port);
        }

        public override int GetHashCode ()
        {
            return port.GetHashCode ();
        }

        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("PortMessage ");
            sb.Append (" Port ");
            sb.Append (port);
            return sb.ToString ();
        }

        #endregion
    }
}
