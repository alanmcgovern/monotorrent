//
// DhtMessage.cs
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


using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Dht.Messages
{
    abstract class DhtMessage : Message
    {
        internal static bool UseVersionKey = true;

        protected static readonly BEncodedString IdKey = "id";
        static readonly BEncodedString TransactionIdKey = "t";
        static readonly BEncodedString VersionKey = "v";
        static readonly BEncodedString MessageTypeKey = "y";
        static readonly BEncodedString DhtVersion = VersionInfo.DhtClientVersion;

        protected BEncodedDictionary properties = new BEncodedDictionary ();

        public BEncodedString ClientVersion
             => (BEncodedString) properties.GetValueOrDefault (VersionKey) ?? BEncodedString.Empty;

        internal abstract NodeId Id {
            get;
        }

        public BEncodedString MessageType => (BEncodedString) properties[MessageTypeKey];

        public BEncodedValue TransactionId {
            get => properties[TransactionIdKey];
            set => properties[TransactionIdKey] = value;
        }


        protected DhtMessage (BEncodedString messageType)
        {
            properties.Add (TransactionIdKey, null);
            properties.Add (MessageTypeKey, messageType);
            if (UseVersionKey)
                properties.Add (VersionKey, DhtVersion);
        }

        protected DhtMessage (BEncodedDictionary dictionary)
        {
            properties = dictionary;
        }

        public override int ByteLength => properties.LengthInBytes ();

        public override void Decode (byte[] buffer, int offset, int length)
        {
            properties = BEncodedValue.Decode<BEncodedDictionary> (buffer, offset, length, false);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            return properties.Encode (buffer, offset);
        }

        public virtual void Handle (DhtEngine engine, Node node)
        {
            node.Seen ();
        }
    }
}
