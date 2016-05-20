using System;
using System.Collections.Generic;

namespace MonoTorrent.Client.Messages
{
    public class MessageBundle : PeerMessage
    {
        public MessageBundle()
        {
            Messages = new List<PeerMessage>();
        }

        public MessageBundle(PeerMessage message)
            : this()
        {
            Messages.Add(message);
        }

        public List<PeerMessage> Messages { get; }

        public override int ByteLength
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Messages.Count; i++)
                    total += Messages[i].ByteLength;
                return total;
            }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            throw new InvalidOperationException();
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            for (var i = 0; i < Messages.Count; i++)
                written += Messages[i].Encode(buffer, written);

            return CheckWritten(written - offset);
        }
    }
}