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
    public class PeerExchangeMessage : ExtensionMessage, IRentable
    {
        public static readonly ExtensionSupport Support = CreateSupport ("ut_pex");

        BEncodedDictionary peerDict;
        static readonly BEncodedString AddedKey = new BEncodedString ("added");
        static readonly BEncodedString AddedDotFKey = new BEncodedString ("added.f");
        static readonly BEncodedString DroppedKey = new BEncodedString ("dropped");

        static readonly BEncodedString Added6Key = new BEncodedString ("added6");
        static readonly BEncodedString Added6DotFKey = new BEncodedString ("added6.f");
        static readonly BEncodedString Dropped6Key = new BEncodedString ("dropped6");

        public override int ByteLength => 4 + 1 + 1 + peerDict.LengthInBytes ();

        public ReadOnlyMemory<byte> Added => ((BEncodedString) peerDict[AddedKey]).AsMemory ();
        public ReadOnlyMemory<byte> AddedDotF => ((BEncodedString) peerDict[AddedDotFKey]).AsMemory ();
        public ReadOnlyMemory<byte> Dropped => ((BEncodedString) peerDict[DroppedKey]).AsMemory ();

        public ReadOnlyMemory<byte> Added6 => ((BEncodedString) peerDict[Added6Key]).AsMemory ();
        public ReadOnlyMemory<byte> Added6DotF => ((BEncodedString) peerDict[Added6DotFKey]).AsMemory ();
        public ReadOnlyMemory<byte> Dropped6 => ((BEncodedString) peerDict[Dropped6Key]).AsMemory ();

        ByteBufferPool.Releaser MemoryReleaser { get; set; }

        public PeerExchangeMessage ()
            : base (Support.MessageId)
        {
            peerDict = new BEncodedDictionary {
                {AddedKey, BEncodedString.Empty },
                {AddedDotFKey, BEncodedString.Empty },
                {DroppedKey, BEncodedString.Empty },
                {Added6Key, BEncodedString.Empty },
                {Added6DotFKey, BEncodedString.Empty },
                {Dropped6Key, BEncodedString.Empty },
            };
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            peerDict = ReadBencodedValue<BEncodedDictionary> (ref buffer, false);
            if (!peerDict.ContainsKey (AddedKey))
                peerDict.Add (AddedKey, BEncodedString.Empty);
            if (!peerDict.ContainsKey (AddedDotFKey))
                peerDict.Add (AddedDotFKey, BEncodedString.Empty);
            if (!peerDict.ContainsKey (DroppedKey))
                peerDict.Add (DroppedKey, BEncodedString.Empty);

            if (!peerDict.ContainsKey (Added6Key))
                peerDict.Add (Added6Key, BEncodedString.Empty);
            if (!peerDict.ContainsKey (Added6DotFKey))
                peerDict.Add (Added6DotFKey, BEncodedString.Empty);
            if (!peerDict.ContainsKey (Dropped6Key))
                peerDict.Add (Dropped6Key, BEncodedString.Empty);
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

        public PeerExchangeMessage Initialize (ExtensionSupports supportedExtensions, ReadOnlyMemory<byte> added, ReadOnlyMemory<byte> addedDotF, ReadOnlyMemory<byte> dropped, ReadOnlyMemory<byte> added6, ReadOnlyMemory<byte> added6DotF, ReadOnlyMemory<byte> dropped6, ByteBufferPool.Releaser memoryReleaser)
        {
            ExtensionId = supportedExtensions.MessageId (Support);

            MemoryReleaser = memoryReleaser;
            peerDict[AddedKey] = BEncodedString.FromMemory (added);
            peerDict[AddedDotFKey] = BEncodedString.FromMemory (addedDotF);
            peerDict[DroppedKey] = BEncodedString.FromMemory (dropped);

            peerDict[Added6Key] = BEncodedString.FromMemory (added6);
            peerDict[Added6DotFKey] = BEncodedString.FromMemory (added6DotF);
            peerDict[Dropped6Key] = BEncodedString.FromMemory (dropped6);

            return this;
        }

        protected override void Reset ()
        {
            ExtensionId = 0;
            peerDict[AddedKey] = peerDict[AddedDotFKey] = peerDict[DroppedKey] = BEncodedString.Empty;
            peerDict[Added6Key] = peerDict[Added6DotFKey] = peerDict[Dropped6Key] = BEncodedString.Empty;
            MemoryReleaser.Dispose ();
            MemoryReleaser = default;
        }

        public override string ToString ()
        {
            var added = (BEncodedString) peerDict[AddedKey];
            int numPeers = added.Span.Length / 6;

            var added6 = (BEncodedString) peerDict[Added6Key];
            int numPeers6 = added.Span.Length / 18;

            return $"PeerExchangeMessage: {numPeers} ipv4 peers. {numPeers6} ipv6 peers.";
        }
    }
}
