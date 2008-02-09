using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public abstract class LibtorrentMessage : PeerMessage
    {
        internal static readonly List<LTSupport> Supports = new List<LTSupport>();

        protected static byte nextId;
        public const byte MessageId = 20;

        static LibtorrentMessage()
        {
            nextId = 1;
            Supports.Add(new LTSupport("LT_chat", nextId++));
            Supports.Add(new LTSupport("LT_metadata", nextId++));
        }

        protected static LTSupport CreateSupport(string name)
        {
            return Supports.Find(delegate(LTSupport s) { return s.Name == name; });
        }

        public new static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public new static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            PeerMessage message;

            byte id = buffer[offset];

            // The first byte tells us what kind of extended message it is
            if (id == ExtendedHandshakeMessage.Support.MessageId)
                message = new ExtendedHandshakeMessage();

            else if (id == LTChat.Support.MessageId)
                message = new LTChat();

            else if (id == LTMetadata.Support.MessageId)
                message = new LTMetadata();

            else
                message = new UnknownMessage();

            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}
