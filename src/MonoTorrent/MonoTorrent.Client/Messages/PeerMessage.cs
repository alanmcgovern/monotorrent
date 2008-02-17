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
        private static Dictionary<byte, CreateMessage> messageDict;

        static PeerMessage()
        {
            messageDict = new Dictionary<byte, CreateMessage>();

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
        }

        public static void Register(byte identifier, CreateMessage creator)
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
            if (buffer[offset] == LibtorrentMessage.MessageId)
                return LibtorrentMessage.DecodeMessage(buffer, offset + 1, count, manager);


            if (messageDict.TryGetValue(buffer[offset], out creator))
                message = creator(manager);
            else
                message = new UnknownMessage();

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message.Decode(buffer, offset + 1, count);
            return message;
        }

        internal abstract void Handle(PeerIdInternal id);
    }
}
