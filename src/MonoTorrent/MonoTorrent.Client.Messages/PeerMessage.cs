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
using System.Net;

using MonoTorrent.Client.Messages.FastPeer;
using MonoTorrent.Client.Messages.Libtorrent;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client.Messages
{
    abstract class PeerMessage : Message
    {
        static readonly Dictionary<byte, Func<ITorrentData, PeerMessage>> messageDict;

        static PeerMessage ()
        {
            messageDict = new Dictionary<byte, Func<ITorrentData, PeerMessage>> ();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received
            Register (AllowedFastMessage.MessageId, data => new AllowedFastMessage ());
            Register (BitfieldMessage.MessageId, data => new BitfieldMessage ((int) Math.Ceiling ((double) data.Size / data.PieceLength)));
            Register (CancelMessage.MessageId, data => new CancelMessage ());
            Register (ChokeMessage.MessageId, data => new ChokeMessage ());
            Register (HaveAllMessage.MessageId, data => new HaveAllMessage ());
            Register (HaveMessage.MessageId, data => new HaveMessage ());
            Register (HaveNoneMessage.MessageId, data => new HaveNoneMessage ());
            Register (InterestedMessage.MessageId, data => new InterestedMessage ());
            Register (NotInterestedMessage.MessageId, data => new NotInterestedMessage ());
            Register (PieceMessage.MessageId, data => new PieceMessage ());
            Register (PortMessage.MessageId, data => new PortMessage ());
            Register (RejectRequestMessage.MessageId, data => new RejectRequestMessage ());
            Register (RequestMessage.MessageId, data => new RequestMessage ());
            Register (SuggestPieceMessage.MessageId, data => new SuggestPieceMessage ());
            Register (UnchokeMessage.MessageId, data => new UnchokeMessage ());

            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection
            Register (ExtensionMessage.MessageId,
                arg => throw new MessageException ("Shouldn't decode extension message this way"));
        }

        static void Register (byte identifier, Func<ITorrentData, PeerMessage> creator)
        {
            if (creator == null)
                throw new ArgumentNullException (nameof (creator));

            lock (messageDict)
                messageDict.Add (identifier, creator);
        }

        public static PeerMessage DecodeMessage (byte[] buffer, int offset, int count, ITorrentData manager)
        {
            if (count < 4)
                throw new ArgumentException ("A message must contain a 4 byte length prefix");

            int messageLength = IPAddress.HostToNetworkOrder (BitConverter.ToInt32 (buffer, offset));

            if (messageLength > (count - 4))
                throw new ArgumentException ("Incomplete message detected");

            if (buffer[offset + 4] == ExtensionMessage.MessageId)
                return ExtensionMessage.DecodeExtensionMessage (buffer, offset + 4 + 1, count - 4 - 1, manager);

            if (!messageDict.TryGetValue (buffer[offset + 4], out Func<ITorrentData, PeerMessage> creator))
                throw new ProtocolException ("Unknown message received");

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            PeerMessage message = creator (manager);
            message.Decode (buffer, offset + 4 + 1, count - 4 - 1);
            return message;
        }

        internal void Handle (TorrentManager manager, PeerId id)
        {
            manager.Mode.HandleMessage (id, this);
        }
    }
}
