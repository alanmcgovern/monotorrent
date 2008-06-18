using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class LTChat : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport("LT_chat");

        private static readonly BEncodedString MessageKey = "msg";
        private BEncodedDictionary messageDict = new BEncodedDictionary();
        public string Message
        {
            set { messageDict[MessageKey] = (BEncodedString)(value ?? ""); }
            get { return ((BEncodedString)messageDict[MessageKey]).Text; }
        }

        internal override void Handle(PeerId id)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            // FIXME: Do nothing for the moment - Maybe raise an event in the future?
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + messageDict.LengthInBytes(); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            messageDict = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            int written = offset;

            written += Write(buffer, offset, ByteLength - 4);
            written += Write(buffer, written, PeerMessage.LibTorrentMessageId);
            written += Write(buffer, written, Support.MessageId);
            written += messageDict.Encode(buffer, written);
            
            CheckWritten(written - offset);
            return written - offset; ;
        }
    }
}
