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


using System;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages;

namespace MonoTorrent.Dht.Messages
{
    abstract class DhtMessage : Message
    {
        internal static bool UseVersionKey = true;

        protected static readonly BEncodedString IdKey = new BEncodedString ("id");
        static readonly BEncodedString TransactionIdKey = new BEncodedString ("t");
        static readonly BEncodedString VersionKey = new BEncodedString ("v");
        static readonly BEncodedString MessageTypeKey = new BEncodedString ("y");
        static readonly BEncodedString DhtVersion = new BEncodedString (GitInfoHelper.DhtClientVersion);

        protected BEncodedDictionary properties = new BEncodedDictionary ();

        public BEncodedString ClientVersion
             => (BEncodedString?) properties.GetValueOrDefault (VersionKey) ?? BEncodedString.Empty;

        internal abstract NodeId Id {
            get;
        }

        public BEncodedString MessageType => (BEncodedString) properties[MessageTypeKey];

        public BEncodedValue? TransactionId {
            get => properties.GetValueOrDefault (TransactionIdKey);
            set {
                if (value == null)
                    properties.Remove (TransactionIdKey);
                else
                    properties[TransactionIdKey] = value;
            }
        }


        protected DhtMessage (BEncodedString messageType)
        {
            properties.Add (MessageTypeKey, messageType);
            if (UseVersionKey)
                properties.Add (VersionKey, DhtVersion);
        }

        protected DhtMessage (BEncodedDictionary dictionary)
        {
            properties = dictionary;
        }

        public override int ByteLength => properties.LengthInBytes ();

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            properties = ReadBencodedValue<BEncodedDictionary> (ref buffer, false);
        }

        public override int Encode (Span<byte> buffer)
        {
            var length = buffer.Length;
            Write (ref buffer, properties);
            return length - buffer.Length;
        }

        public virtual void Handle (DhtEngine engine, Node node)
        {
            node.Seen ();
        }
    }
}
