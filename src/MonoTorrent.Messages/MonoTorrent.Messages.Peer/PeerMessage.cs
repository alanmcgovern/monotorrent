//
// PeerMessage.cs
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
using System.Threading;

using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;

namespace MonoTorrent.Messages.Peer
{
    partial class PeerMessage
    {
        public readonly struct Releaser : IDisposable
        {
            readonly PeerMessage Message;
            readonly Action<PeerMessage> MessageReleaser;
            readonly int ReuseId;

            internal Releaser (PeerMessage message, Action<PeerMessage> messageReleaser)
                => (Message, ReuseId, MessageReleaser) = (message, message.ReuseId, messageReleaser);

            public void Dispose ()
            {
                if (Message != null) {
                    if (Message.ReuseId != ReuseId)
                        throw new InvalidOperationException ("Double free detected for this message");

                    Message.ReuseId++;
                    Message.Reset ();
                    MessageReleaser (Message);
                }
            }
        }
    }

    static class PeerMessageCache<T>
        where T : PeerMessage, IRentable, new()
    {
        static readonly SpinLocked<Stack<T>> Cache = SpinLocked.Create (new Stack<T> ());

        static readonly Action<PeerMessage> ReturnMessage = (message) => {
            using (Cache.Enter (out var cache))
                cache.Push ((T) message);
        };

        public static (T, PeerMessage.Releaser) GetOrCreate ()
        {
            T message;
            using (Cache.Enter (out var cache)) {
                if (cache.Count > 0)
                    message = cache.Pop ();
                else
                    message = new T ();
            }

            return (message, new PeerMessage.Releaser (message, ReturnMessage));
        }

    }

    public abstract partial class PeerMessage : Message
    {
        int ReuseId;

        static readonly object locker = new object ();
        static Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>?[] messages;

        static PeerMessage ()
        {
            messages = new Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>[0];

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received

            // These standard messages are singletons as they are stateless.
            Register (ChokeMessage.MessageId, _ => (ChokeMessage.Instance, default));
            Register (UnchokeMessage.MessageId, _ => (UnchokeMessage.Instance, default));
            Register (InterestedMessage.MessageId, _ => (InterestedMessage.Instance, default));
            Register (NotInterestedMessage.MessageId, _ => (NotInterestedMessage.Instance, default));

            // These FastMessages are singletons as they are stateless.
            Register (HaveAllMessage.MessageId, _ => (HaveAllMessage.Instance, default));
            Register (HaveNoneMessage.MessageId, _ => (HaveNoneMessage.Instance, default));

            // Cacheable standard messages
            Register (HaveMessage.MessageId, _ => PeerMessageCache<HaveMessage>.GetOrCreate ());
            Register (RequestMessage.MessageId, _ => PeerMessageCache<RequestMessage>.GetOrCreate ());
            Register (PieceMessage.MessageId, _ => PeerMessageCache<PieceMessage>.GetOrCreate ());
            Register (CancelMessage.MessageId, _ => PeerMessageCache<CancelMessage>.GetOrCreate ());
            Register (PortMessage.MessageId, _ => PeerMessageCache<PortMessage>.GetOrCreate ());

            // Cacheable FastMessages
            Register (AllowedFastMessage.MessageId, _ => PeerMessageCache<AllowedFastMessage>.GetOrCreate ());
            Register (SuggestPieceMessage.MessageId, _ => PeerMessageCache<SuggestPieceMessage>.GetOrCreate ());
            Register (RejectRequestMessage.MessageId, _ => PeerMessageCache<RejectRequestMessage>.GetOrCreate ());

            // Cacheable bittorrent v2 messages
            Register (HashRejectMessage.MessageId, _ => PeerMessageCache<HashRejectMessage>.GetOrCreate ());
            Register (HashesMessage.MessageId, _ => PeerMessageCache<HashesMessage>.GetOrCreate ());

            // Currently uncached
            Register (BitfieldMessage.MessageId, data => (data?.TorrentInfo == null ? BitfieldMessage.UnknownLength : new BitfieldMessage (data.TorrentInfo.PieceCount ()), default));
            Register (HashRequestMessage.MessageId, data => (new HashRequestMessage (), default));
        }

        public static (T peerMessage, Releaser releaser) Rent<T> ()
            where T : PeerMessage, IRentable, new()
            => PeerMessageCache<T>.GetOrCreate ();

        private protected static void Register (byte identifier, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)> creator)
        {
            lock (locker) {
                if (messages.Length <= identifier)
                    Array.Resize (ref messages, identifier + 1);
                if (!(messages[identifier] is null))
                    throw new InvalidOperationException ($"Double registration of message id {identifier}");
                messages[identifier] = creator;
            }
        }

        public static (PeerMessage message, Releaser releaser) DecodeMessage (ReadOnlySpan<byte> buffer, ITorrentManagerInfo? manager)
        {
            if (buffer.Length < 4)
                throw new ArgumentException ("A message must contain a 4 byte length prefix");

            int messageLength = ReadInt (ref buffer);
            if (messageLength > buffer.Length)
                throw new ArgumentException ("Incomplete message detected");

            if (buffer[0] == ExtensionMessage.MessageId)
                return ExtensionMessage.DecodeExtensionMessage (buffer.Slice (1), manager);

            var registeredMessages = messages;
            if (buffer[0] >= registeredMessages.Length)
                throw new MessageException ("Unknown message received");

            var creator = registeredMessages[buffer[0]];
            if (creator is null)
                throw new MessageException ("Unknown message received");

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            (var message, var releaser) = creator (manager);
            message.Decode (buffer.Slice (1));
            return (message, releaser);
        }

        protected virtual void Reset ()
        {

        }
    }
}
