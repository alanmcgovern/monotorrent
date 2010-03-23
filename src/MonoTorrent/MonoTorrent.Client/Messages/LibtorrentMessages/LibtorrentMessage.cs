//
// LibtorrentMessage.cs
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
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public abstract class ExtensionMessage : PeerMessage
    {
        internal static readonly byte MessageId = 20;
        private static Dictionary<byte, CreateMessage> messageDict;
        private static byte nextId;

        internal static readonly List<ExtensionSupport> SupportedMessages = new List<ExtensionSupport>();

        private byte extensionId;

        public byte ExtensionId
        {
            get { return extensionId; }
            protected set { extensionId = value; }
        }

        static ExtensionMessage()
        {
            messageDict = new Dictionary<byte, CreateMessage>();

            Register(nextId++, delegate { return new ExtendedHandshakeMessage(); });

            Register(nextId, delegate { return new LTChat(); });
            SupportedMessages.Add(new ExtensionSupport("LT_chat", nextId++));

            Register(nextId, delegate { return new LTMetadata(); });
            SupportedMessages.Add(new ExtensionSupport("ut_metadata", nextId++));

            Register(nextId, delegate { return new PeerExchangeMessage(); });
            SupportedMessages.Add(new ExtensionSupport("ut_pex", nextId++));
        }

        public ExtensionMessage(byte messageId)
        {
            this.extensionId = messageId;
        }

        public static void Register(byte identifier, CreateMessage creator)
        {
            if (creator == null)
                throw new ArgumentNullException("creator");

            lock (messageDict)
                messageDict.Add(identifier, creator);
        }

        protected static ExtensionSupport CreateSupport(string name)
        {
            return SupportedMessages.Find(delegate(ExtensionSupport s) { return s.Name == name; });
        }

        public new static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            CreateMessage creator;
            PeerMessage message;

            if (!ClientEngine.SupportsExtended)
                throw new MessageException("Extension messages are not supported");

            if (!messageDict.TryGetValue(buffer[offset], out creator))
                throw new ProtocolException("Unknown extension message received");

            message = creator(manager);
            message.Decode(buffer, offset + 1, count - 1);
            return message;
        }
    }
}
