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
        public static readonly ExtensionSupport Support = CreateSupport ("ut_metadata");
        static readonly BEncodedString MessageTypeKey = "msg_type";
        static readonly BEncodedString PieceKey = "piece";
        static readonly BEncodedString TotalSizeKey = "total_size";
        public static readonly int BlockSize = 16 * 1024;

        public enum MessageType
        {
            Request = 0,
            Data = 1,
            Reject = 2
        }

        readonly BEncodedDictionary dict;

        //this buffer contain all metadata when we send message 
        //and only a piece of metadata we receive message

        public int Piece { get; set; }

        public byte[] MetadataPiece { get; set; }

        public MessageType MetadataMessageType { get; internal set; }

        //only for register
        public LTMetadata ()
            : base (Support.MessageId)
        {

        }

        public LTMetadata (ExtensionSupports supportedExtensions, MessageType type, int piece)
            : this (supportedExtensions, type, piece, null)
        {

        }

        public LTMetadata (ExtensionSupports supportedExtensions, MessageType type, int piece, byte[] metadata)
            : this (supportedExtensions.MessageId (Support), type, piece, metadata)
        {

        }

        public LTMetadata (byte extensionId, MessageType type, int piece, byte[] metadata)
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
                if (metadata is null)
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

        public override void Decode (byte[] buffer, int offset, int length)
        {
            using var reader = new MemoryStream (buffer, offset, length, false);
            BEncodedDictionary d = BEncodedValue.Decode<BEncodedDictionary> (reader, false);
            int totalSize = 0;

            if (d.TryGetValue (MessageTypeKey, out BEncodedValue val))
                MetadataMessageType = (MessageType) ((BEncodedNumber) val).Number;
            if (d.TryGetValue (PieceKey, out val))
                Piece = (int) ((BEncodedNumber) val).Number;
            if (d.TryGetValue (TotalSizeKey, out val)) {
                totalSize = (int) ((BEncodedNumber) val).Number;
                MetadataPiece = new byte[Math.Min (totalSize - Piece * BlockSize, BlockSize)];
                reader.Read (MetadataPiece, 0, MetadataPiece.Length);
            }
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, ByteLength - 4);
            written += Write (buffer, written, MessageId);
            written += Write (buffer, written, ExtensionId);
            written += dict.Encode (buffer, written);
            if (MetadataMessageType == MessageType.Data)
                written += Write (buffer, written, MetadataPiece, Piece * BlockSize, Math.Min (MetadataPiece.Length - Piece * BlockSize, BlockSize));

            return written - offset;
        }
    }
}
