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

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    public class PeerExchangeMessage : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport ("ut_pex");

        BEncodedDictionary peerDict;
        static readonly BEncodedString AddedKey = "added";
        static readonly BEncodedString AddedDotFKey = "added.f";
        static readonly BEncodedString DroppedKey = "dropped";

        public ReadOnlyMemory<byte> Added  => ((BEncodedString) peerDict[AddedKey]).AsMemory ();

        public ReadOnlyMemory<byte> AddedDotF => ((BEncodedString) peerDict[AddedDotFKey]).AsMemory ();

        public ReadOnlyMemory<byte> Dropped => ((BEncodedString) peerDict[DroppedKey]).AsMemory ();

        public PeerExchangeMessage ()
            : base (Support.MessageId)
        {
            peerDict = new BEncodedDictionary ();
        }

        public PeerExchangeMessage (byte messageId, byte[] added, byte[] addedDotF, byte[] dropped)
            : this ()
        {
            ExtensionId = messageId;
            Initialise (added, addedDotF, dropped);
        }

        public PeerExchangeMessage (ExtensionSupports supportedExtensions, byte[] added, byte[] addedDotF, byte[] dropped)
            : this ()
        {
            ExtensionId = supportedExtensions.MessageId (Support);
            Initialise (added, addedDotF, dropped);
        }

        void Initialise (byte[] added, byte[] addedDotF, byte[] dropped)
        {
            added = ((byte[]) added?.Clone ()) ?? Array.Empty<byte> ();
            addedDotF = ((byte[]) addedDotF?.Clone ()) ?? Array.Empty<byte> ();
            dropped = ((byte[]) dropped?.Clone ()) ?? Array.Empty<byte> ();

            peerDict[AddedKey] = BEncodedString.FromMemory (added);
            peerDict[AddedDotFKey] = BEncodedString.FromMemory (addedDotF);
            peerDict[DroppedKey] = BEncodedString.FromMemory (dropped);
        }

        public override int ByteLength => 4 + 1 + 1 + peerDict.LengthInBytes ();

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            peerDict = ReadBencodedValue<BEncodedDictionary> (ref buffer, false);
            if (!peerDict.ContainsKey (AddedKey))
                peerDict.Add (AddedKey, BEncodedString.Empty);
            if (!peerDict.ContainsKey (AddedDotFKey))
                peerDict.Add (AddedDotFKey, BEncodedString.Empty);
            if (!peerDict.ContainsKey (DroppedKey))
                peerDict.Add (DroppedKey, BEncodedString.Empty);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, ByteLength - 4);
            Write (ref buffer, MessageId);
            Write (ref buffer, ExtensionId);
            Write (ref buffer, peerDict);

            return written - buffer.Length;
        }

        public override string ToString ()
        {
            var added = (BEncodedString) peerDict[AddedKey];
            int numPeers = added.Span.Length / 6;

            return $"PeerExchangeMessage: {numPeers} peers";
        }
    }
}
