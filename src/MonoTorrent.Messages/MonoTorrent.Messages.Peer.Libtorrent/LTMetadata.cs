//
// LTMetadata.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.IO;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    public class LTMetadata : ExtensionMessage
    {
        static readonly BEncodedDictionary EmptyDict = new BEncodedDictionary ();

        public static readonly ExtensionSupport Support = CreateSupport ("ut_metadata");
        static readonly BEncodedString MessageTypeKey = new BEncodedString ("msg_type");
        static readonly BEncodedString PieceKey = new BEncodedString ("piece");
        static readonly BEncodedString TotalSizeKey = new BEncodedString ("total_size");
        public static readonly int BlockSize = 16 * 1024;

        public enum MessageType
        {
            Request = 0,
            Data = 1,
            Reject = 2
        }

        BEncodedDictionary dict;

        //this buffer contain all metadata when we send message 
        //and only a piece of metadata we receive message

        public int Piece { get; set; }

        public ReadOnlyMemory<byte> MetadataPiece { get; set; }

        public MessageType MetadataMessageType { get; internal set; }

        //only for register
        public LTMetadata ()
            : base (Support.MessageId)
        {
            dict = EmptyDict;
            MetadataPiece = Array.Empty<byte> ();
        }

        public LTMetadata (ExtensionSupports supportedExtensions, MessageType type, int piece)
            : this (supportedExtensions, type, piece, Array.Empty<byte> ())
        {

        }

        public LTMetadata (ExtensionSupports supportedExtensions, MessageType type, int piece, ReadOnlyMemory<byte> metadata)
            : this (supportedExtensions.MessageId (Support), type, piece, metadata)
        {

        }

        public LTMetadata (byte extensionId, MessageType type, int piece, ReadOnlyMemory<byte> metadata)
            : this ()
        {
            ExtensionId = extensionId;
            MetadataMessageType = type;
            MetadataPiece = metadata;
            Piece = piece;

            dict = new BEncodedDictionary {
                { MessageTypeKey, (BEncodedNumber) (int) MetadataMessageType },
                { PieceKey, (BEncodedNumber) piece }
            };

            if (MetadataMessageType == MessageType.Data) {
                if (metadata.IsEmpty)
                    throw new InvalidDataException ("The metadata data message did not contain any data.");
                dict.Add (TotalSizeKey, (BEncodedNumber) metadata.Length);
            }
        }

        public override int ByteLength {
            // 4 byte length, 1 byte BT id, 1 byte LT id, 1 byte payload
            get {
                int length = 4 + 1 + 1 + dict.LengthInBytes ();
                if (MetadataMessageType == MessageType.Data)
                    length += Math.Min (MetadataPiece.Length - Piece * BlockSize, BlockSize);
                return length;
            }
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            var d = dict = ReadBencodedValue<BEncodedDictionary> (ref buffer, false);

            if (d.TryGetValue (MessageTypeKey, out BEncodedValue? val))
                MetadataMessageType = (MessageType) ((BEncodedNumber) val).Number;
            if (d.TryGetValue (PieceKey, out val))
                Piece = (int) ((BEncodedNumber) val).Number;
            if (d.TryGetValue (TotalSizeKey, out val)) {
                int totalSize = (int) ((BEncodedNumber) val).Number;
                var metadataPiece = new byte[Math.Min (totalSize - Piece * BlockSize, BlockSize)];
                buffer.CopyTo (metadataPiece);
                MetadataPiece = metadataPiece;
            }
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, ByteLength - 4);
            Write (ref buffer, MessageId);
            Write (ref buffer, ExtensionId);
            Write (ref buffer, dict);

            if (MetadataMessageType == MessageType.Data) {
                var total = Math.Min (MetadataPiece.Length - Piece * BlockSize, BlockSize);
                MetadataPiece.Span.Slice (Piece * BlockSize, total).CopyTo (buffer);
                buffer = buffer.Slice (total);
            }
            return written - buffer.Length;
        }
    }
}
