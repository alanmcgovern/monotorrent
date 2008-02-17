using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public abstract class LibtorrentMessage : PeerMessage
    {
		private static Dictionary<byte, CreateMessage> messageDict;

        internal static readonly List<LTSupport> SupportedMessages = new List<LTSupport>();

        protected static byte nextId;
        public const byte MessageId = 20;

        static LibtorrentMessage()
        {
			messageDict = new Dictionary<byte, CreateMessage>();
			nextId = 1;

			Register(ExtendedHandshakeMessage.MessageId, delegate { return new ExtendedHandshakeMessage(); });

			Register(nextId, delegate { return new LTChat(); });
            SupportedMessages.Add(new LTSupport("LT_chat", nextId++));

			Register(nextId, delegate { return new LTMetadata(); });
			SupportedMessages.Add(new LTSupport("LT_metadata", nextId++));
        }

        protected static LTSupport CreateSupport(string name)
        {
			return SupportedMessages.Find(delegate(LTSupport s) { return s.Name == name; });
        }

        public new static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public new static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
			CreateMessage creator;
            PeerMessage message;

            byte id = buffer[offset];
			if (!messageDict.TryGetValue(buffer[offset], out creator))
				message = new UnknownMessage();
			else
				message = creator();

            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}
