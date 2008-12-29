using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client.Messages
{
    public delegate PeerMessage CreateMessage(TorrentManager manager);
    public abstract class PeerMessage : Message
    {
        internal const byte LibTorrentMessageId = 20;
        private static Dictionary<byte, CreateMessage> messageDict;

        static PeerMessage()
        {
            messageDict = new Dictionary<byte, CreateMessage>();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received
            Register(AllowedFastMessage.MessageId,   delegate (TorrentManager manager) { return new AllowedFastMessage(); });
            Register(BitfieldMessage.MessageId,      delegate (TorrentManager manager) { return new BitfieldMessage(manager.Torrent.Pieces.Count); });
            Register(CancelMessage.MessageId,        delegate (TorrentManager manager) { return new CancelMessage(); });
            Register(ChokeMessage.MessageId,         delegate (TorrentManager manager) { return new ChokeMessage(); });
            Register(HaveAllMessage.MessageId,       delegate (TorrentManager manager) { return new HaveAllMessage(); });
            Register(HaveMessage.MessageId,          delegate (TorrentManager manager) { return new HaveMessage(); });
            Register(HaveNoneMessage.MessageId,      delegate (TorrentManager manager) { return new HaveNoneMessage(); });
            Register(InterestedMessage.MessageId,    delegate (TorrentManager manager) { return new InterestedMessage(); });
            Register(NotInterestedMessage.MessageId, delegate (TorrentManager manager) { return new NotInterestedMessage(); });
            Register(PieceMessage.MessageId,         delegate (TorrentManager manager) { return new PieceMessage(manager); });
            Register(PortMessage.MessageId,          delegate (TorrentManager manager) { return new PortMessage(); });
            Register(RejectRequestMessage.MessageId, delegate (TorrentManager manager) { return new RejectRequestMessage(); });
            Register(RequestMessage.MessageId,       delegate (TorrentManager manager) { return new RequestMessage(); });
            Register(SuggestPieceMessage.MessageId,  delegate (TorrentManager manager) { return new SuggestPieceMessage(); });
            Register(UnchokeMessage.MessageId,       delegate (TorrentManager manager) { return new UnchokeMessage(); });
            
            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection
            Register(LibTorrentMessageId, delegate(TorrentManager manager) { return new UnknownMessage(); });
        }

        private static void Register(byte identifier, CreateMessage creator)
        {
            if (creator == null)
                throw new ArgumentNullException("creator");

            lock (messageDict)
                messageDict.Add(identifier, creator);
        }

        public static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            PeerMessage message;
            CreateMessage creator;

            if (count < 4)
                throw new ArgumentException("A message must contain a 4 byte length prefix");

            int messageLength = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, offset));

            if (messageLength > (count - 4))
                throw new ArgumentException("Incomplete message detected");

            if (buffer[offset + 4] == LibTorrentMessageId)
                return ExtensionMessage.DecodeMessage(buffer, offset + 4 + 1, count - 4 - 1, manager);

            if (!messageDict.TryGetValue(buffer[offset + 4], out creator))
                return new UnknownMessage();

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message = creator(manager);
            message.Decode(buffer, offset + 4 + 1, count - 4 - 1);
            return message;
        }

        internal abstract void Handle(PeerId id);
    }
}
