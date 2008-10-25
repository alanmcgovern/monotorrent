//
// Message.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2008 Alan McGovern
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

using MonoTorrent.BEncoding;
using System.Net;
using MonoTorrent.Common;

namespace MonoTorrent.Dht.Messages
{
    internal abstract class Message : MonoTorrent.Client.Messages.Message
    {
        protected static readonly BEncodedString IdKey = "id";
        private static BEncodedString TransactionIdKey = "t";
        private static BEncodedString VersionKey = "v";
        private static BEncodedString MessageTypeKey = "y";
        private static BEncodedString DhtVersion = VersionInfo.DhtClientVersion;

        protected BEncodedDictionary properties = new BEncodedDictionary();

        public BEncodedString ClientVersion
        {
            get { return (BEncodedString)properties[VersionKey]; }
        }

        internal abstract NodeId Id
        {
            get;
        }

        public BEncodedString MessageType
        {
            get { return (BEncodedString)properties[MessageTypeKey]; }
        }

        public BEncodedString TransactionId
        {
            get { return (BEncodedString)properties[TransactionIdKey]; }
            set { properties[TransactionIdKey] = value; }
        }


        protected Message(BEncodedString messageType)
        {
            properties.Add(TransactionIdKey, null);
            properties.Add(MessageTypeKey, messageType);
            properties.Add(VersionKey, DhtVersion);
        }

        protected Message(BEncodedDictionary dictionary)
        {
            properties = dictionary;
            if (!properties.ContainsKey(VersionKey))
                properties.Add(VersionKey, null);
        }

        public override int ByteLength
        {
            get { return properties.LengthInBytes(); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            properties = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            return properties.Encode(buffer, offset);
        }

        public virtual void Handle(DhtEngine engine, Node node)
        {
            node.Seen();
        }
    }
}
