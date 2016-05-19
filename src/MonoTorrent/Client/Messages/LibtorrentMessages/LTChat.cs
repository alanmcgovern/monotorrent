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

        public LTChat()
            : base(Support.MessageId)
        {

        }

        internal LTChat(byte messageId, string message)
            : this()
        {
            ExtensionId = messageId;
            Message = message;
        }

        public LTChat(PeerId peer, string message)
            : this()
        {
            ExtensionId = peer.ExtensionSupports.MessageId(Support);
            Message = message;
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + messageDict.LengthInBytes(); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            messageDict = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            int written = offset;

            written += Write(buffer, offset, ByteLength - 4);
            written += Write(buffer, written, ExtensionMessage.MessageId);
            written += Write(buffer, written, ExtensionId);
            written += messageDict.Encode(buffer, written);
            
            CheckWritten(written - offset);
            return written - offset; ;
        }
    }
}
