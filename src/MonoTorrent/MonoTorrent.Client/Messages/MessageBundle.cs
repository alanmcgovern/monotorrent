using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages
{
    public class MessageBundle : PeerMessage
    {
        private List<PeerMessage> messages;

        public List<PeerMessage> Messages
        {
            get { return messages; }
        }

        public MessageBundle()
        {
            messages = new List<PeerMessage>();
        }

        internal override void Handle(PeerIdInternal id)
        {
            throw new InvalidOperationException();
        }

        public override int ByteLength
        {
            get
            {
                int total = 0;
                messages.ForEach(delegate(PeerMessage m) { total += m.ByteLength; });
                return total;
            }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            throw new InvalidOperationException();
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;
            messages.ForEach(delegate(PeerMessage m) { written += m.Encode(buffer, written); });
            
            CheckWritten(written - offset);
            return written - offset;
        }
    }
}
