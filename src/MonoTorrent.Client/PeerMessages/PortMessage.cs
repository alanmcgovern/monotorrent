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

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// Represents a "Port" message
    /// </summary>
    public class PortMessage : IPeerMessage
    {
        private const int messageLength = 3;
        private const int messageId = 9;

        #region Member Variables
        /// <summary>
        /// The port
        /// </summary>
        public uint Port
        {
            get { return this.port; }
        }
        private uint port;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new PortMessage
        /// </summary>
        public PortMessage()
        {
        }


        /// <summary>
        /// Creates a new Port Message
        /// </summary>
        /// <param name="port">The port to use</param>
        public PortMessage(ushort port)
        {
            this.port = port;
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Encodes the PortMessage into the supplied buffer
        /// </summary>
        /// <param name="id">The peer who we are about to send the message to</param>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        public int Encode(byte[] buffer, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(messageLength)), 0, buffer, offset, 4);
            buffer[offset + 4] = (byte)messageId;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.port)), 0, buffer, offset + 5, 2);

            return (messageLength + 4);
        }


        /// <summary>
        /// Decodes a Portessage from the supplied buffer
        /// </summary>
        /// <param name="id">The peer to decode the message from</param>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        public void Decode(byte[] buffer, int offset, int length)
        {
            this.port = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToUInt16(buffer, offset));
        }
        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        public void Handle(PeerConnectionID id)
        {
            //Debug.WriteLine(DateTime.Now + ": No support for handling " + this.ToString() + " messages");
        }

        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        public int ByteLength
        {
            get { return (messageLength + 4); }
        }
        #endregion


        #region Overridden methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "PortMessage";
        }

        public override bool Equals(object obj)
        {
            PortMessage msg = obj as PortMessage;
            if (msg == null)
                return false;

            return (this.port == msg.port);
        }

        public override int GetHashCode()
        {
            return this.port.GetHashCode();
        }
        #endregion
    }
}