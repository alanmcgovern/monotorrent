using System;
using System.Collections.Generic;
using System.Net;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages
{
    public delegate PeerMessage CreateMessage(TorrentManager manager);

    public abstract class PeerMessage : Message
    {
        private static readonly Dictionary<byte, CreateMessage> messageDict;

        static PeerMessage()
        {
            messageDict = new Dictionary<byte, CreateMessage>();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received
            Register(AllowedFastMessage.MessageId, delegate { return new AllowedFastMessage(); });
            Register(BitfieldMessage.MessageId,
                delegate(TorrentManager manager) { return new BitfieldMessage(manager.Bitfield.Length); });
            Register(CancelMessage.MessageId, delegate { return new CancelMessage(); });
            Register(ChokeMessage.MessageId, delegate { return new ChokeMessage(); });
            Register(HaveAllMessage.MessageId, delegate { return new HaveAllMessage(); });
            Register(HaveMessage.MessageId, delegate { return new HaveMessage(); });
            Register(HaveNoneMessage.MessageId, delegate { return new HaveNoneMessage(); });
            Register(InterestedMessage.MessageId, delegate { return new InterestedMessage(); });
            Register(NotInterestedMessage.MessageId,
                delegate { return new NotInterestedMessage(); });
            Register(PieceMessage.MessageId, delegate { return new PieceMessage(); });
            Register(PortMessage.MessageId, delegate { return new PortMessage(); });
            Register(RejectRequestMessage.MessageId,
                delegate { return new RejectRequestMessage(); });
            Register(RequestMessage.MessageId, delegate { return new RequestMessage(); });
            Register(SuggestPieceMessage.MessageId,
                delegate { return new SuggestPieceMessage(); });
            Register(UnchokeMessage.MessageId, delegate { return new UnchokeMessage(); });

            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection
            Register(ExtensionMessage.MessageId,
                delegate { throw new MessageException("Shouldn't decode extension message this way"); });
        }

        private static void Register(byte identifier, CreateMessage creator)
        {
            if (creator == null)
                throw new ArgumentNullException("creator");

            lock (messageDict)
                messageDict.Add(identifier, creator);
        }

        public static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            PeerMessage message;
            CreateMessage creator;

            if (count < 4)
                throw new ArgumentException("A message must contain a 4 byte length prefix");

            var messageLength = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, offset));

            if (messageLength > count - 4)
                throw new ArgumentException("Incomplete message detected");

            if (buffer[offset + 4] == ExtensionMessage.MessageId)
                return ExtensionMessage.DecodeMessage(buffer, offset + 4 + 1, count - 4 - 1, manager);

            if (!messageDict.TryGetValue(buffer[offset + 4], out creator))
                throw new ProtocolException("Unknown message received");

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message = creator(manager);
            message.Decode(buffer, offset + 4 + 1, count - 4 - 1);
            return message;
        }

        internal void Handle(PeerId id)
        {
            id.TorrentManager.Mode.HandleMessage(id, this);
        }
    }
}