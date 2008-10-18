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

        public MessageBundle(PeerMessage message)
            : this()
        {
            messages.Add(message);
        }

        internal override void Handle(PeerId id)
        {
            throw new InvalidOperationException();
        }

        public override int ByteLength
        {
            get
            {
                int total = 0;
                for (int i = 0; i < messages.Count; i++)
                    total += messages[i].ByteLength;
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
            
            for (int i = 0; i < messages.Count; i++)
                written += messages[i].Encode(buffer, written);
            
            CheckWritten(written - offset);
            return written - offset;
        }
    }
}
