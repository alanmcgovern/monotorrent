using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using System.Net;

namespace MonoTorrent.Client.Messages
{
    public abstract class PeerMessage : Message
    {
        internal abstract void Handle(PeerIdInternal id);

        public static PeerMessage DecodeMessage(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            return DecodeMessage(buffer.Array, buffer.Offset + offset, count, manager);
        }

        public static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            PeerMessage message;

            // The first byte tells us what kind of message it is
            switch (buffer[offset])
            {
                case AllowedFastMessage.MessageId:
                    message = new AllowedFastMessage();
                    break;

                case BitfieldMessage.MessageId:
                    message = new BitfieldMessage(manager.Torrent.Pieces.Count);
                    break;

                case CancelMessage.MessageId:
                    message = new CancelMessage();
                    break;

                case ChokeMessage.MessageId:
                    message = new ChokeMessage();
                    break;

                case ExtendedListMessage.MessageId:
                    message = new ExtendedListMessage();
                    break;

                case HaveAllMessage.MessageId:
                    message = new HaveAllMessage();
                    break;

                case HaveMessage.MessageId:
                    message = new HaveMessage();
                    break;

                case HaveNoneMessage.MessageId:
                    message = new HaveNoneMessage();
                    break;

                case InterestedMessage.MessageId:
                    message = new InterestedMessage();
                    break;

                case NotInterestedMessage.MessageId:
                    message = new NotInterestedMessage();
                    break;

                case PieceMessage.MessageId:
                    message = new PieceMessage(manager);
                    break;

                case PortMessage.MessageId:
                    message = new PortMessage();
                    break;

                case RejectRequestMessage.MessageId:
                    message = new RejectRequestMessage();
                    break;

                case RequestMessage.MessageId:
                    message = new RequestMessage();
                    break;

                case SuggestPieceMessage.MessageId:
                    message = new SuggestPieceMessage();
                    break;

                case UnchokeMessage.MessageId:
                    message = new UnchokeMessage();
                    break;


                case 21:                            // An "extended" message
                    switch (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset + 1)))
                    {
                        default:
                            throw new NotSupportedException();
                    }

                default:
                    message = new UnknownMessage();
                    break;
            }

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}
