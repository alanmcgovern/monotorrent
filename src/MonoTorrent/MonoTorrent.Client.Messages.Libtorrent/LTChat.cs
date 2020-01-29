//
// LTChat.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    class LTChat : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport ("LT_chat");

        static readonly BEncodedString MessageKey = "msg";
        BEncodedDictionary messageDict = new BEncodedDictionary ();
        public string Message {
            set => messageDict[MessageKey] = (BEncodedString) (value ?? "");
            get => ((BEncodedString) messageDict[MessageKey]).Text;
        }

        public LTChat ()
            : base (Support.MessageId)
        {

        }

        internal LTChat (byte messageId, string message)
            : this ()
        {
            ExtensionId = messageId;
            Message = message;
        }

        public LTChat (PeerId peer, string message)
            : this ()
        {
            ExtensionId = peer.ExtensionSupports.MessageId (Support);
            Message = message;
        }

        public override int ByteLength => 4 + 1 + 1 + messageDict.LengthInBytes ();

        public override void Decode (byte[] buffer, int offset, int length)
        {
            messageDict = BEncodedValue.Decode<BEncodedDictionary> (buffer, offset, length, false);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException ("Libtorrent extension messages not supported");

            int written = offset;

            written += Write (buffer, offset, ByteLength - 4);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, ExtensionId);
            written += messageDict.Encode (buffer, written);

            CheckWritten (written - offset);
            return written - offset;
            ;
        }
    }
}
