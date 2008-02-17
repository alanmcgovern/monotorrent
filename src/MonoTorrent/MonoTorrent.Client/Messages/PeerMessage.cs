using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client.Messages
{
    public delegate PeerMessage CreateMessage();
    public abstract class PeerMessage : Message
    {
        private static Dictionary<byte, CreateMessage> messageDict;

        static PeerMessage()
        {
            messageDict = new Dictionary<byte, CreateMessage>();

            Register(AllowedFastMessage.MessageId,        delegate { return new AllowedFastMessage(); });
            //Register(BitfieldMessage.MessageId,        delegate { return new BitfieldMessage(); });
            Register(CancelMessage.MessageId,            delegate { return new CancelMessage(); });
            Register(ChokeMessage.MessageId,            delegate { return new ChokeMessage(); });
            Register(HaveAllMessage.MessageId,            delegate { return new HaveAllMessage(); });
            Register(HaveMessage.MessageId,                delegate { return new HaveMessage(); });
            Register(HaveNoneMessage.MessageId,            delegate { return new HaveNoneMessage(); });
            Register(InterestedMessage.MessageId,        delegate { return new InterestedMessage(); });
            Register(NotInterestedMessage.MessageId,    delegate { return new NotInterestedMessage(); });
            //Register(PieceMessage.MessageId,            delegate { return new PieceMessage(); });
            Register(PortMessage.MessageId,                delegate { return new PortMessage(); });
            Register(RejectRequestMessage.MessageId,    delegate { return new RejectRequestMessage(); });
            Register(RequestMessage.MessageId,            delegate { return new RequestMessage(); });
            Register(SuggestPieceMessage.MessageId,        delegate { return new SuggestPieceMessage(); });
            Register(UnchokeMessage.MessageId,            delegate { return new UnchokeMessage(); });
        }

        public static void Register(byte identifier, CreateMessage creator)
        {
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

            switch (buffer[offset])
            {
                case LibtorrentMessage.MessageId:
                    return LibtorrentMessage.DecodeMessage(buffer, offset + 1, count, manager);

                case BitfieldMessage.MessageId:
                    message = new BitfieldMessage(manager.Torrent.Pieces.Count);
                    break;

                case PieceMessage.MessageId:
                    message = new PieceMessage(manager);
                    break;

                default:
                    if (!messageDict.TryGetValue(buffer[offset], out creator))
                        message = new UnknownMessage();
                    else
                        message = creator();
                    break;
            }

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message.Decode(buffer, offset + 1, count);
            return message;
        }

        internal abstract void Handle(PeerIdInternal id);
    }
}
