using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class LTMetadata : LibtorrentMessage
    {
        internal static readonly LTSupport Support = CreateSupport("LT_metadata");

        private byte messageType;
        private byte start;
        private byte size;

        private int totalSize;
        private int offset;
        private byte[] metadata;

        private byte expectedId;
        internal override void Handle(PeerIdInternal id)
        {
            if (messageType == 0)
            {
                expectedId = id.Connection.LTSupports.Find(delegate(LTSupport l) { return l.Name == Support.Name; }).MessageId;
                id.Connection.Enqueue(this);
            }
        }

        public override int ByteLength
        {
            // 4 byte length, 1 byte BT id, 1 byte LT id, 1 byte payload
            get { return 4 + 1 + 1 + 1; }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            // FIXME: Do this properly
            messageType = buffer[offset];
            if (messageType < 0 || messageType > 2)
                throw new MessageException(string.Format("Invalid messagetype in LTMetadata: {0}", messageType));
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, LibtorrentMessage.MessageId);
            written += Write(buffer, written, expectedId);
            written += Write(buffer, written, 2); // FIXME: We always say we have no metadata

            return written - offset;
        }
    }
}
