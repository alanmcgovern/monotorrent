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
        internal static readonly byte MessageId = 20;
        static readonly Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> messageDict;

        internal static readonly List<ExtensionSupport> SupportedMessages = new List<ExtensionSupport> ();

        public byte ExtensionId { get; protected set; }

        static ExtensionMessage ()
        {
            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection.
            Register<ExtensionMessage> (MessageId, data => throw new MessageException ("Shouldn't decode extension message this way"), false);

            messageDict = new Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> ();

            byte id = Register (data => new ExtendedHandshakeMessage (), false);
            if (id != 0)
                throw new InvalidOperationException ("The handshake message should be registered with id '0'");

            id = Register (data => GetInstance<LTChat> (), false);
            SupportedMessages.Add (new ExtensionSupport ("LT_chat", id));

            id = Register (data => GetInstance<LTMetadata> (), false);
            SupportedMessages.Add (new ExtensionSupport ("ut_metadata", id));

            id = Register (data => GetInstance<PeerExchangeMessage> (), false);
            SupportedMessages.Add (new ExtensionSupport ("ut_pex", id));
        }

        protected ExtensionMessage (byte messageId)
        {
            ExtensionId = messageId;
        }

        public static byte Register<T> (Func<ITorrentManagerInfo?, T> creator, bool reusable)
            where T : PeerMessage
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            lock (messageDict) {
                byte id = (byte) messageDict.Count;
                Func<ITorrentManagerInfo?, (PeerMessage, Releaser)> wrapper;
                if (reusable) {
                    lock (InstanceCache)
                        InstanceCache[typeof (T)] = new Queue<PeerMessage> ();
                    wrapper = (data) => { var message = creator (data); return (message, new Releaser (message)); };
                } else
                    wrapper = (data) => (creator (data), default);
                messageDict.Add (id, wrapper);
                return id;
            }
        }

        protected static ExtensionSupport CreateSupport (string name)
        {
            return SupportedMessages.Find (s => s.Name == name);
        }

        public static (PeerMessage message, Releaser releaser) DecodeExtensionMessage (ReadOnlySpan<byte> buffer, ITorrentManagerInfo? manager)
        {
            if (!messageDict.TryGetValue (buffer[0], out Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>? creator))
                throw new MessageException ("Unknown extension message received");

            (PeerMessage message, Releaser releaser) = creator (manager);
            message.Decode (buffer.Slice (1));
            return (message, releaser);
        }
    }
}
