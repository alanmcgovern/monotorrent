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
using System.Net;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// Represents a "Port" message
    /// </summary>
    public class PortMessage : PeerMessage
    {
        private const int messageLength = 3;
        public const byte MessageId = 9;

        #region Private Fields
        private ushort port;
        #endregion


        #region Public Properties

        public override int ByteLength
        {
            get { return (messageLength + 4); }
        }

        public int Port
        {
            get { return this.port; }
        }

        #endregion


        #region Constructors
        public PortMessage()
        {
        }

        public PortMessage(ushort port)
        {
            this.port = port;
        }
        
        #endregion


        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            this.port = (ushort)ReadShort(buffer, offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
			int written = offset;

			written += Write(buffer, written, messageLength);
			written += Write(buffer, written, MessageId);
			written += Write(buffer, written, port);

            CheckWritten(written - offset);
            return written - offset;
        }

        public override bool Equals(object obj)
        {
            PortMessage msg = obj as PortMessage;
            return (msg == null) ? false : (this.port == msg.port);
        }

        public override int GetHashCode()
        {
            return this.port.GetHashCode();
        }

        internal override void Handle(PeerId id)
        {
            id.Port = this.port;
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("PortMessage ");
            sb.Append(" Port ");
            sb.Append(this.port);
            return sb.ToString();
        }

        #endregion
    }
}