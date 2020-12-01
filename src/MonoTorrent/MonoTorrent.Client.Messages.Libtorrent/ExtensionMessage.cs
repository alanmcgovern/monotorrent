//
// ExtensionMessage.cs
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

namespace MonoTorrent.Client.Messages.Libtorrent
{
    abstract class ExtensionMessage : PeerMessage
    {
        internal static readonly byte MessageId = 20;
        static readonly Dictionary<byte, Func<ITorrentData, PeerMessage>> messageDict;

        internal static readonly List<ExtensionSupport> SupportedMessages = new List<ExtensionSupport> ();

        public byte ExtensionId { get; protected set; }

        static ExtensionMessage ()
        {
            messageDict = new Dictionary<byte, Func<ITorrentData, PeerMessage>> ();

            byte id = Register (data => new ExtendedHandshakeMessage ());
            if (id != 0)
                throw new InvalidOperationException ("The handshake message should be registered with id '0'");

            id = Register (data => new LTChat ());
            SupportedMessages.Add (new ExtensionSupport ("LT_chat", id));

            id = Register (data => new LTMetadata ());
            SupportedMessages.Add (new ExtensionSupport ("ut_metadata", id));

            id = Register (data => new PeerExchangeMessage ());
            SupportedMessages.Add (new ExtensionSupport ("ut_pex", id));
        }

        protected ExtensionMessage (byte messageId)
        {
            ExtensionId = messageId;
        }

        public static byte Register (Func<ITorrentData, PeerMessage> creator)
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            lock (messageDict) {
                byte id = (byte) messageDict.Count;
                messageDict.Add (id, creator);
                return id;
            }
        }

        protected static ExtensionSupport CreateSupport (string name)
        {
            return SupportedMessages.Find (s => s.Name == name);
        }

        public static PeerMessage DecodeExtensionMessage (byte[] buffer, int offset, int count, ITorrentData manager)
        {
            if (!ClientEngine.SupportsExtended)
                throw new MessageException ("Extension messages are not supported");

            if (!messageDict.TryGetValue (buffer[offset], out Func<ITorrentData, PeerMessage> creator))
                throw new ProtocolException ("Unknown extension message received");

            PeerMessage message = creator (manager);
            message.Decode (buffer, offset + 1, count - 1);
            return message;
        }
    }
}
