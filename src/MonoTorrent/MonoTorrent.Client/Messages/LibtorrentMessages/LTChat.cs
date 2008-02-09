using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class LTChat : LibtorrentMessage
    {
        internal static readonly LTSupport Support = CreateSupport("LT_chat");

        private static readonly BEncodedString MessageKey = "msg";
        private string message;

        public string Message
        {
            set { message = value ?? ""; }
            get { return message; }
        }

        internal override void Handle(PeerIdInternal id)
        {
            // FIXME: Do nothing for the moment - Maybe raise an event in the future?
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + Encoding.UTF8.GetByteCount(message); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedDictionary d = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length);
            message = d[MessageKey].ToString();
        }

        public override int Encode(byte[] buffer, int offset)
        {
            BEncodedDictionary d = new BEncodedDictionary();
            d.Add(MessageKey, (BEncodedString)message);
            CheckWritten(d.Encode(buffer, offset));
            return ByteLength;
        }
    }
}
