//
// ExtendedListMessage.cs
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
using System.Text;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// This class represents the BT_EXTENDED_LST as listed by the Azurues Extended Messaging Protocol
    /// </summary>
    public class ExtendedListMessage : IPeerMessageInternal, IPeerMessage
    {
        public const byte MessageId = (byte)20;

        #region Member Variables
        public BEncodedDictionary Dictionary
        {
            get { return this.dictionary; }
        }
        private BEncodedDictionary dictionary;
        #endregion


        #region Constructors
        public ExtendedListMessage()
        {
        }
        public ExtendedListMessage(BEncodedDictionary supportedMessages)
        {
        }
        #endregion


        #region Methods
        internal int Encode(ArraySegment<byte> buffer, int offset)
        {
            throw new ProtocolException("The method or operation is not implemented.");
        }

        internal void Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.dictionary = (BEncodedDictionary)BEncodedValue.Decode(buffer.Array, buffer.Offset + offset, length);
        }

        internal void Handle(PeerId id)
        {
            throw new ProtocolException("The method or operation is not implemented.");
        }

        public int ByteLength
        {

            get { throw new ProtocolException("The method or operation is not implemented."); }
        }
        #endregion

       
        #region IPeerMessageInternal Explicit Calls

        int IPeerMessageInternal.Encode(ArraySegment<byte> buffer, int offset)
        {
            return this.Encode(buffer, offset);
        }

        void IPeerMessageInternal.Decode(ArraySegment<byte> buffer, int offset, int length)
        {
            this.Decode(buffer, offset, length);
        }

        void IPeerMessageInternal.Handle(PeerId id)
        {
            this.Handle(id);
        }

        int IPeerMessageInternal.ByteLength
        {
            get { return this.ByteLength; }
        }

        #endregion
    }
}
