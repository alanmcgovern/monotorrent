using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public abstract class ExtensionMessage : PeerMessage
    {
        private static readonly byte HandshakeMessageId = 0;
        private static Dictionary<byte, CreateMessage> messageDict;

        internal static readonly List<ExtensionSupport> SupportedMessages = new List<ExtensionSupport>();

        private static byte nextId;

        static ExtensionMessage()
        {
            HandshakeMessageId = 0;
            nextId = 1;

            messageDict = new Dictionary<byte, CreateMessage>();

            Register(HandshakeMessageId, delegate { return new ExtendedHandshakeMessage(); });

            Register(nextId, delegate { return new LTChat(); });
            SupportedMessages.Add(new ExtensionSupport("LT_chat", nextId++));

            Register(nextId, delegate { return new LTMetadata(); });
            SupportedMessages.Add(new ExtensionSupport("LT_metadata", nextId++));

            Register(nextId, delegate { return new PeerExchangeMessage(); });
            SupportedMessages.Add(new ExtensionSupport("ut_pex", nextId++));
        }

        public static void Register(byte identifier, CreateMessage creator)
        {
            if (creator == null)
                throw new ArgumentNullException("creator");

            lock (messageDict)
                messageDict.Add(identifier, creator);
        }

        protected static ExtensionSupport CreateSupport(string name)
        {
            return SupportedMessages.Find(delegate(ExtensionSupport s) { return s.Name == name; });
        }

        public new static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public new static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            CreateMessage creator;
            PeerMessage message;

            if (!messageDict.TryGetValue(buffer[offset], out creator))
                return new UnknownMessage();

            message = creator(manager);
            message.Decode(buffer, offset + 1, count - 1);
            return message;
        }
    }
}
