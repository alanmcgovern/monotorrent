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


namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// Represents a "Port" message
    /// </summary>
    class PortMessage : PeerMessage
    {
        const int messageLength = 3;
        internal static readonly byte MessageId = 9;

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

        public override void Decode (byte[] buffer, int offset, int length)
        {
            port = (ushort) ReadShort (buffer, ref offset);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, messageLength);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, port);

            return CheckWritten (written - offset);
        }

        public override bool Equals (object obj)
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