//
// HaveNoneMessage.cs
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
using System.Net;

namespace MonoTorrent.Client.Messages.PeerMessages
{
    public class HaveNoneMessage : MonoTorrent.Client.Messages.PeerMessage
    {
        public const byte MessageId = 0x0F;
        private readonly int messageLength = 1;

        #region Constructors
        public HaveNoneMessage()
        {
        }
        #endregion


        #region Methods
        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message encoding not supported");
            
            int written = Write(buffer, offset, messageLength);
            written += Write(buffer, offset + 4, MessageId);

            CheckWritten(written);
            return written;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new ProtocolException("Message decoding not supported");
        }

        internal override void Handle(PeerIdInternal id)
        {
            if (!id.Connection.SupportsFastPeer)
                throw new MessageException("Peer shouldn't support fast peer messages");

            id.Connection.BitField.SetAll(false);
            id.Peer.IsSeeder = false;
            id.TorrentManager.SetAmInterestedStatus(id, false);
        }


        public override int ByteLength
        {
            get { return this.messageLength + 4; }
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            return obj is HaveNoneMessage;
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public override string ToString()
        {
            return "HaveNoneMessage";
        }
        #endregion
    }
}
