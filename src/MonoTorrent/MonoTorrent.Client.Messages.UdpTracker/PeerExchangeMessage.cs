//
// PeerExchangeMessage.cs
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

namespace MonoTorrent.Client.Messages.Libtorrent
{
    class PeerExchangeMessage : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport ("ut_pex");

        BEncodedDictionary peerDict;
        static readonly BEncodedString AddedKey = "added";
        static readonly BEncodedString AddedDotFKey = "added.f";
        static readonly BEncodedString DroppedKey = "dropped";

        public PeerExchangeMessage ()
            : base (Support.MessageId)
        {
            peerDict = new BEncodedDictionary ();
        }

        internal PeerExchangeMessage (byte messageId, byte[] added, byte[] addedDotF, byte[] dropped)
            : this ()
        {
            ExtensionId = messageId;
            Initialise (added, addedDotF, dropped);
        }

        public PeerExchangeMessage (PeerId id, byte[] added, byte[] addedDotF, byte[] dropped)
            : this ()
        {
            ExtensionId = id.ExtensionSupports.MessageId (Support);
            Initialise (added, addedDotF, dropped);
        }

        void Initialise (byte[] added, byte[] addedDotF, byte[] dropped)
        {
            peerDict[AddedKey] = (BEncodedString) (added ?? Array.Empty<byte> ());
            peerDict[AddedDotFKey] = (BEncodedString) (addedDotF ?? Array.Empty<byte> ());
            peerDict[DroppedKey] = (BEncodedString) (dropped ?? Array.Empty<byte> ());
        }

        public byte[] Added {
            set => peerDict[AddedKey] = (BEncodedString) (value ?? Array.Empty<byte> ());
            get => ((BEncodedString) peerDict[AddedKey]).TextBytes;
        }

        public byte[] AddedDotF {
            set => peerDict[AddedDotFKey] = (BEncodedString) (value ?? Array.Empty<byte> ());
            get => ((BEncodedString) peerDict[AddedDotFKey]).TextBytes;
        }

        public byte[] Dropped {
            set => peerDict[DroppedKey] = (BEncodedString) (value ?? Array.Empty<byte> ());
            get => ((BEncodedString) peerDict[DroppedKey]).TextBytes;
        }

        public override int ByteLength => 4 + 1 + 1 + peerDict.LengthInBytes ();

        public override void Decode (byte[] buffer, int offset, int length)
        {
            peerDict = BEncodedValue.Decode<BEncodedDictionary> (buffer, offset, length, false);
            if (!peerDict.ContainsKey (AddedKey))
                peerDict.Add (AddedKey, (BEncodedString) "");
            if (!peerDict.ContainsKey (AddedDotFKey))
                peerDict.Add (AddedDotFKey, (BEncodedString) "");
            if (!peerDict.ContainsKey (DroppedKey))
                peerDict.Add (DroppedKey, (BEncodedString) "");
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, offset, ByteLength - 4);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, ExtensionId);
            written += peerDict.Encode (buffer, written);

            return CheckWritten (written - offset);
        }

        public override string ToString ()
        {
            var added = (BEncodedString) peerDict[AddedKey];
            int numPeers = added.TextBytes.Length / 6;

            return $"PeerExchangeMessage: {numPeers} peers";
        }
    }
}
