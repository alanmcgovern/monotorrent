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
        where T : PeerMessage
    {
        static Queue<T>? InstanceCache;
        static Func<T>? Creator;

        public static void Init (Func<T> creator)
        {
            InstanceCache = InstanceCache ?? new Queue<T> ();
            Creator = Creator ?? creator;
        }

        static readonly Action<PeerMessage> ReturnMessage = (message) => {
            lock (InstanceCache!)
                InstanceCache.Enqueue ((T) message);
        };

        public static (T, PeerMessage.Releaser) GetOrCreate ()
        {
            if (InstanceCache == null)
                return (Creator! (), default);

            T message = null!;
            lock (InstanceCache) {
                if (InstanceCache.Count > 0)
                    message = InstanceCache.Dequeue ();
                else
                    message = Creator! ();
            }
            
            return (message, new PeerMessage.Releaser (message, ReturnMessage));
        }

    }

    public abstract partial class PeerMessage : Message
    {
        int ReuseId;

        static readonly Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> messageDict;

        static PeerMessage ()
        {
            // bundle messages are always cacheable.
            PeerMessageCache<HaveBundle>.Init (() => new HaveBundle ());
            PeerMessageCache<MessageBundle>.Init (() => new MessageBundle ());
            PeerMessageCache<RequestBundle>.Init (() => new RequestBundle ());
            PeerMessageCache<AllowedFastBundle>.Init (() => new AllowedFastBundle ());

            // These built-in messages are always cacheable
            PeerMessageCache<PieceMessage>.Init (() => new PieceMessage ());
            PeerMessageCache<RequestMessage>.Init (() => new RequestMessage ());
            PeerMessageCache<CancelMessage>.Init (() => new CancelMessage ());
            PeerMessageCache<AllowedFastMessage>.Init (() => new AllowedFastMessage ());
            PeerMessageCache<HaveMessage>.Init (() => new HaveMessage ());
            PeerMessageCache<SuggestPieceMessage>.Init (() => new SuggestPieceMessage ());
            PeerMessageCache<PortMessage>.Init (() => new PortMessage ());
            PeerMessageCache<RejectRequestMessage>.Init (() => new RejectRequestMessage ());
            PeerMessageCache<HashRejectMessage>.Init (() => new HashRejectMessage ());
            PeerMessageCache<HashesMessage>.Init (() => new HashesMessage ());


            messageDict = new Dictionary<byte, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)>> ();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received


            // These aren't marked as cachable. They're singletons.
            Register (ChokeMessage.MessageId, data => (ChokeMessage.Instance, default));
            Register (HaveAllMessage.MessageId, data => (HaveAllMessage.Instance, default));
            Register (HaveNoneMessage.MessageId, data => (HaveNoneMessage.Instance, default));
            Register (InterestedMessage.MessageId, data => (InterestedMessage.Instance, default));
            Register (NotInterestedMessage.MessageId, data => (NotInterestedMessage.Instance, default));
            Register (UnchokeMessage.MessageId, data => (UnchokeMessage.Instance, default));

            // Cache most/all messages
            Register (PieceMessage.MessageId, data => PeerMessageCache<PieceMessage>.GetOrCreate ());
            Register (RequestMessage.MessageId, data => PeerMessageCache<RequestMessage>.GetOrCreate ());
            Register (CancelMessage.MessageId, data => PeerMessageCache<CancelMessage>.GetOrCreate ());
            Register (AllowedFastMessage.MessageId, data => PeerMessageCache<AllowedFastMessage>.GetOrCreate ());
            Register (HaveMessage.MessageId, data => PeerMessageCache<HaveMessage>.GetOrCreate ());
            Register (SuggestPieceMessage.MessageId, data => PeerMessageCache<SuggestPieceMessage>.GetOrCreate ());
            Register (PortMessage.MessageId, data => PeerMessageCache<PortMessage>.GetOrCreate ());
            Register (RejectRequestMessage.MessageId, data => PeerMessageCache<RejectRequestMessage>.GetOrCreate ());
            Register (HashRejectMessage.MessageId, data => PeerMessageCache<HashRejectMessage>.GetOrCreate ());
            Register (HashesMessage.MessageId, data => PeerMessageCache<HashesMessage>.GetOrCreate ());

            // Currently uncached
            Register (BitfieldMessage.MessageId, data => (data?.TorrentInfo == null ? BitfieldMessage.UnknownLength : new BitfieldMessage (data.TorrentInfo.PieceCount ()), default));
            Register (HashRequestMessage.MessageId, data => (new HashRequestMessage (), default));
        }

        public static (T peerMessage, Releaser releaser) Rent<T> ()
            where T : PeerMessage, new()
            => PeerMessageCache<T>.GetOrCreate ();

        public static Releaser Rent<T> (out T message)
            where T : PeerMessage, new()
        {
            (var msg, var releaser) = Rent<T> ();
            message = msg;
            return releaser;
        }

        private protected static void Register (byte identifier, Func<ITorrentManagerInfo?, (PeerMessage, Releaser)> creator)
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            lock (messageDict)
                messageDict.Add (identifier, creator);
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
