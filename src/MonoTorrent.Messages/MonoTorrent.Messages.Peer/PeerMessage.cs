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

using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;

namespace MonoTorrent.Messages.Peer
{
    partial class PeerMessage
    {
        public readonly struct Releaser : IDisposable
        {
            readonly PeerMessage Message;
            readonly int ReuseId;

            internal Releaser (PeerMessage message)
                => (Message, ReuseId) = (message, message.ReuseId);

            public void Dispose ()
            {
                if (Message != null) {
                    if (Message.ReuseId != ReuseId)
                        throw new InvalidOperationException ("Double free detected for this message");

                    Message.ReuseId++;
                    Message.Reset ();
                    lock (InstanceCache) {
                        if (InstanceCache.TryGetValue (Message.GetType (), out var queue))
                            queue.Enqueue (Message);
                    }
                }
            }
        }
    }

    public abstract partial class PeerMessage : Message
    {
        int ReuseId;

        private protected static readonly Dictionary<Type, Queue<PeerMessage>> InstanceCache;
        static readonly Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> messageDict;

        static PeerMessage ()
        {
            // These two are always cacheable.
            InstanceCache = new Dictionary<Type, Queue<PeerMessage>> {
                { typeof (HaveBundle), new Queue<PeerMessage> () },
                { typeof (MessageBundle), new Queue<PeerMessage> () },
                { typeof (RequestBundle), new Queue<PeerMessage> () }
            };
            messageDict = new Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> ();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received


            // These aren't marked as cachable. They're singletons.
            Register (ChokeMessage.MessageId, data => ChokeMessage.Instance, false);
            Register (HaveAllMessage.MessageId, data => HaveAllMessage.Instance, false);
            Register (HaveNoneMessage.MessageId, data => HaveNoneMessage.Instance, false);
            Register (InterestedMessage.MessageId, data => InterestedMessage.Instance, false);
            Register (NotInterestedMessage.MessageId, data => NotInterestedMessage.Instance, false);
            Register (UnchokeMessage.MessageId, data => UnchokeMessage.Instance, false);

            // Cache most/all messages
            Register (PieceMessage.MessageId, data => GetInstance<PieceMessage> (), true);
            Register (RequestMessage.MessageId, data => GetInstance<RequestMessage> (), true);
            Register (CancelMessage.MessageId, data => GetInstance<CancelMessage> (), true);
            Register (AllowedFastMessage.MessageId, data => GetInstance<AllowedFastMessage> (), true);
            Register (HaveMessage.MessageId, data => GetInstance<HaveMessage> (), true);
            Register (SuggestPieceMessage.MessageId, data => GetInstance<SuggestPieceMessage> (), true);
            Register (PortMessage.MessageId, data => GetInstance<PortMessage> (), true);

            // Currently uncached
            Register (BitfieldMessage.MessageId, data => data?.TorrentInfo == null ? BitfieldMessage.UnknownLength : new BitfieldMessage (data.TorrentInfo.PieceCount ()));
            Register (RejectRequestMessage.MessageId, data => GetInstance<RejectRequestMessage> ());
            Register (HashRequestMessage.MessageId, data => GetInstance<HashRequestMessage> ());
            Register (HashesMessage.MessageId, data => GetInstance<HashesMessage> ());
            Register (HashRejectMessage.MessageId, data => GetInstance<HashRejectMessage> ());
        }


        protected static T GetInstance<T> ()
            where T : PeerMessage, new()
        {
            lock (InstanceCache)
                if (InstanceCache.TryGetValue (typeof (T), out var cache))
                    return cache.Count > 0 ? (T) cache.Dequeue () : new T ();
            return new T ();
        }

        public static (T peerMessage, Releaser releaser) Rent<T> ()
            where T : PeerMessage, new()
        {
            var instance = GetInstance<T> ();
            return (instance, new Releaser (instance));
        }

        public static Releaser Rent<T> (out T message)
            where T : PeerMessage, new()
        {
            (var msg, var releaser) = Rent<T> ();
            message = msg;
            return releaser;
        }

        static void Register<T> (byte identifier, Func<ITorrentManagerInfo?, T> creator)
            where T: PeerMessage
            => Register (identifier, creator, false);

        private protected static void Register<T> (byte identifier, Func<ITorrentManagerInfo?, T> creator, bool reusable)
            where T : PeerMessage
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            Func<ITorrentManagerInfo?, (PeerMessage, Releaser)> wrapper;
            if (reusable) {
                lock (InstanceCache)
                    InstanceCache[typeof (T)] = new Queue<PeerMessage> ();
                wrapper = (data) => { var msg = creator (data); return (msg, new Releaser (msg)); };
            } else {
                wrapper = (data) => (creator (data), default);
            }

            lock (messageDict)
                messageDict.Add (identifier, wrapper);
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

            if (!messageDict.TryGetValue (buffer[0], out Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>? creator))
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
