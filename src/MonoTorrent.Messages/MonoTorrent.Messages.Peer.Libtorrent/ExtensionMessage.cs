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

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    public abstract class ExtensionMessage : PeerMessage
    {
        internal const byte MessageId = 20;

        static readonly object locker = new object ();
        static Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>?[] messages;

        internal static readonly List<ExtensionSupport> SupportedMessages;

        public byte ExtensionId { get; protected set; }

        static ExtensionMessage ()
        {
            SupportedMessages = new List<ExtensionSupport> ();

            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection.
            Register (MessageId, data => throw new MessageException ("Shouldn't decode extension message this way"));
            messages = new Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>?[0];

            byte id = Register (data => (new ExtendedHandshakeMessage (), default));
            if (id != 0)
                throw new InvalidOperationException ("The handshake message should be registered with id '0'");

            id = Register (data => (new LTChat (), default));
            SupportedMessages.Add (new ExtensionSupport ("LT_chat", id));

            id = Register (data => (new LTMetadata (), default));
            SupportedMessages.Add (new ExtensionSupport ("ut_metadata", id));

            id = Register (data => PeerMessageCache<PeerExchangeMessage>.GetOrCreate ());
            SupportedMessages.Add (new ExtensionSupport ("ut_pex", id));
        }

        protected ExtensionMessage (byte messageId)
        {
            ExtensionId = messageId;
        }

        public static byte Register (Func<ITorrentManagerInfo?, (PeerMessage, Releaser)> creator)
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            lock (locker) {
                byte id = (byte) messages.Length;
                Array.Resize (ref messages, id + 1);
                messages[id] = creator;
                return id;
            }
        }

        protected static ExtensionSupport CreateSupport (string name)
        {
            return SupportedMessages.Find (s => s.Name == name);
        }

        public static (PeerMessage message, Releaser releaser) DecodeExtensionMessage (ReadOnlySpan<byte> buffer, ITorrentManagerInfo? manager)
        {
            var registeredMessages = messages;
            if (buffer[0] >= registeredMessages.Length)
                throw new MessageException ("Unknown extension message received");

            var creator = registeredMessages[buffer[0]];
            if (creator is null)
                throw new MessageException ("Unknown extension message received");

            (PeerMessage message, Releaser releaser) = creator (manager);
            message.Decode (buffer.Slice (1));
            return (message, releaser);
        }
    }
}
