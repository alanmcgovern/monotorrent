using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;
using System.Security.Cryptography;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class LTMetadata : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport("LT_metadata");
        private static readonly BEncodedString MessageTypeKey = "msg_type";
        private static readonly BEncodedString PieceKey = "piece";
        private static readonly BEncodedString TotalSizeKey = "total_size";
        private static readonly int BLOCK_SIZE = 16000;

        public enum eMessageType {
            Request = 0,
            Data = 1,
            Reject = 2
        }

        private eMessageType messageType;
        private int piece;

        private int offset;
        private byte[] metadata;

        private byte messageId;

        public LTMetadata()
        {
            this.piece = 0;
            this.messageType = eMessageType.Request;
        }

        internal override void Handle(PeerIdInternal id)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            messageId = id.Connection.ExtensionSupports.Find(delegate(ExtensionSupport l) { return l.Name == Support.Name; }).MessageId;
            
            switch (messageType) {
                case eMessageType.Request :
                     if (false )//&& id.TorrentManager.Torrent.haveAll)
                         messageType = eMessageType.Data;
                     else
                         messageType = eMessageType.Reject;
                     id.Connection.Enqueue(this);//only send the piece requested
                     break;
                case eMessageType.Data :
                    if ((piece + 1) * BLOCK_SIZE < metadata.Length) // if not last piece request another
                    {
                        messageType = eMessageType.Request;
                        piece++;
                        id.Connection.Enqueue(this);
                    }
                    else
                    { 
                        byte[] infoHash = new SHA1Managed().ComputeHash(metadata);
                        if (id.TorrentManager.Torrent.InfoHash != infoHash)
                            return;//invalid metadata received from other peers

                        BEncodedDictionary d = (BEncodedDictionary)BEncodedDictionary.Decode(metadata);
                        //id.TorrentManager.Torrent.ProcessInfo (d);
                        //id.TorrentManager.Torrent.haveAll = true;
                        id.TorrentManager.Start();
                    }
                     break;
                case eMessageType.Reject :
                    break;//do nothing when rejected or flood until other peer send the missing piece? 
                default :
                    throw new MessageException(string.Format("Invalid messagetype in LTMetadata: {0}", messageType));
                    break;
            }

        }

        public override int ByteLength
        {
            // 4 byte length, 1 byte BT id, 1 byte LT id, 1 byte payload
            get { //TODO depend of message type and of value
                return 4 + 1 + 1 + 1;
            }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedValue val;
            BEncodedDictionary d = BEncodedDictionary.Decode<BEncodedDictionary>(buffer, offset, length);
            int totalSize = 0;

            if (d.TryGetValue(MessageTypeKey, out val))
                messageType = (eMessageType)((BEncodedNumber)val).Number;
            if (d.TryGetValue(PieceKey, out val))
                piece = (int)((BEncodedNumber)val).Number;
            if (d.TryGetValue(TotalSizeKey, out val)) {
                totalSize = (int)((BEncodedNumber)val).Number;
                if (metadata == null)
                    metadata = new byte[totalSize];//create empty buffer
                if (offset + d.LengthInBytes() < length)
                    Buffer.BlockCopy(buffer, offset + d.LengthInBytes(), metadata, piece * BLOCK_SIZE, Math.Min(totalSize - piece * BLOCK_SIZE,BLOCK_SIZE));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            int written = offset;

            written += Write(buffer, written, PeerMessage.LibTorrentMessageId);
            written += Write(buffer, written, messageId);

            BEncodedDictionary dict = new BEncodedDictionary();
            dict.Add(MessageTypeKey, (BEncodedNumber)(int)messageType);
            dict.Add(PieceKey, (BEncodedNumber)piece);
            
            if (messageType == eMessageType.Data)
            {
                dict.Add(TotalSizeKey, (BEncodedNumber)metadata.Length);
                written += dict.Encode(buffer, written);
                written += Write(buffer, written, metadata, piece * BLOCK_SIZE, Math.Min(metadata.Length - piece * BLOCK_SIZE,BLOCK_SIZE));
            }
            else
                written += dict.Encode(buffer, written);

            CheckWritten(written - offset);
            return written - offset;
        }
    }
}
