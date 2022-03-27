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


using System;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    public class LTChat : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport ("LT_chat");

        static readonly BEncodedString MessageKey = new BEncodedString ("msg");
        BEncodedDictionary messageDict = new BEncodedDictionary ();
        public string Message {
            set => messageDict[MessageKey] = string.IsNullOrEmpty (value) ? BEncodedString.Empty : new BEncodedString (value);
            get => ((BEncodedString) messageDict[MessageKey]).Text;
        }

        public LTChat ()
            : base (Support.MessageId)
        {

        }

        /// <summary>
        /// </summary>
        /// <param name="supportedExtensions"></param>
        /// <param name="message"></param>
        public LTChat (ExtensionSupports supportedExtensions, string message)
            : this ()
        {
            ExtensionId = supportedExtensions.MessageId (Support);
            Message = message;
        }

        /// <summary>
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="message"></param>
        public LTChat (byte messageId, string message)
            : this ()
        {
            ExtensionId = messageId;
            Message = message;
        }

        public override int ByteLength => 4 + 1 + 1 + messageDict.LengthInBytes ();

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            messageDict = ReadBencodedValue<BEncodedDictionary> (ref buffer, false);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, ByteLength - 4);
            Write (ref buffer, MessageId);
            Write (ref buffer, ExtensionId);
            Write (ref buffer, messageDict);

            return written - buffer.Length;
        }
    }
}
