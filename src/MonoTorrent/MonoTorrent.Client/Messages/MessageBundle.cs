using System;
using System.Collections.Generic;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client.Messages
{
    class MessageBundle : PeerMessage
    {
        public List<PeerMessage> Messages { get; }

        public MessageBundle()
        {
            Messages = new List<PeerMessage>();
        }

        public MessageBundle(PeerMessage message)
            : this()
        {
            Messages.Add(message);
        }

        internal MessageBundle (IList<PieceRequest> requests)
            : this ()
        {
            foreach (var m in requests)
                Messages.Add (new RequestMessage (m.PieceIndex, m.StartOffset, m.RequestLength));
        }

        public override int ByteLength {
            get {
                int total = 0;
                for (int i = 0; i < Messages.Count; i++)
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
            int written = offset;
            
            for (int i = 0; i < Messages.Count; i++)
                written += Messages[i].Encode(buffer, written);

            return CheckWritten(written - offset);
        }
    }
}
