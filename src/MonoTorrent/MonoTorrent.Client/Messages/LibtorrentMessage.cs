using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.PeerMessages;

namespace MonoTorrent.Client.Messages
{
    public abstract class LibtorrentMessage : PeerMessage
    {
        public const byte MessageId = 20;

        public new static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public new static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            PeerMessage message;

            // The first byte tells us what kind of extended message it is
            switch (buffer[offset])
            {
                case ExtendedHandshakeMessage.MessageId:
                    message = new ExtendedHandshakeMessage();
                    break;

                default:
                    message = new UnknownMessage();
                    break;
            }

            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}
