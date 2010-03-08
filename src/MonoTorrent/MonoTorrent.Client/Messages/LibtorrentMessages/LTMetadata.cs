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
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;
using System.Security.Cryptography;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    internal class LTMetadata : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport("ut_metadata");
        private static readonly BEncodedString MessageTypeKey = "msg_type";
        private static readonly BEncodedString PieceKey = "piece";
        private static readonly BEncodedString TotalSizeKey = "total_size";
        internal static readonly int BlockSize = 16384;//16Kb

        internal enum eMessageType {
            Request = 0,
            Data = 1,
            Reject = 2
        }

        BEncodedDictionary dict;
        private eMessageType messageType;
        private int piece;

        //this buffer contain all metadata when we send message 
        //and only a piece of metadata we receive message
        private byte[] metadata;

        public int Piece
        {
            get { return piece; }
        }

        public byte[] MetadataPiece
        {
            get { return metadata; }
        }

        internal eMessageType MetadataMessageType
        {
            get { return messageType; }
        }

        //only for register
        public LTMetadata()
            : base(Support.MessageId)
        {

        }

        public LTMetadata(PeerId id, eMessageType type, int piece)
            : this (id, type, piece, null)
        {

        }

        public LTMetadata(PeerId id, eMessageType type, int piece, byte[] metadata)
            : this(id.ExtensionSupports.MessageId(Support), type, piece, metadata)
        {

        }

        public LTMetadata(byte extensionId, eMessageType type, int piece, byte[] metadata)
            : this()
        {
            ExtensionId = extensionId;
            this.messageType = type;
            this.metadata = metadata;
            this.piece = piece;

            dict = new BEncodedDictionary();
            dict.Add(MessageTypeKey, (BEncodedNumber)(int)messageType);
            dict.Add(PieceKey, (BEncodedNumber)piece);

            if (messageType == eMessageType.Data)
            {
                Check.Metadata(metadata);
                dict.Add(TotalSizeKey, (BEncodedNumber)metadata.Length);
            }
        }

        public override int ByteLength
        {
            // 4 byte length, 1 byte BT id, 1 byte LT id, 1 byte payload
            get {
                int length = 4 + 1 + 1 + dict.LengthInBytes();
                if (messageType == eMessageType.Data)
                    length += Math.Min(metadata.Length - piece * BlockSize, BlockSize);
                return length;
            }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedValue val;
            using (RawReader reader = new RawReader(new MemoryStream(buffer, offset, length, false), false))
            {
                BEncodedDictionary d = BEncodedDictionary.Decode<BEncodedDictionary>(reader);
                int totalSize = 0;

                if (d.TryGetValue(MessageTypeKey, out val))
                    messageType = (eMessageType)((BEncodedNumber)val).Number;
                if (d.TryGetValue(PieceKey, out val))
                    piece = (int)((BEncodedNumber)val).Number;
                if (d.TryGetValue(TotalSizeKey, out val))
                {
                    totalSize = (int)((BEncodedNumber)val).Number;
                    metadata = new byte[Math.Min(totalSize - piece * BlockSize, BlockSize)];
                    reader.Read(metadata, 0, metadata.Length);
                }
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            int written = offset;
            
            written += Write(buffer, written, ByteLength - 4);
            written += Write(buffer, written, ExtensionMessage.MessageId);
            written += Write(buffer, written, ExtensionId);
            written += dict.Encode(buffer, written);
            if (messageType == eMessageType.Data)
                written += Write(buffer, written, metadata, piece * BlockSize, Math.Min(metadata.Length - piece * BlockSize, BlockSize));

            return CheckWritten(written - offset);
        }
    }
}
