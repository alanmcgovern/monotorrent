//
// ExtendedHandshakeMessage.cs
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


using System.Collections.Generic;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    sealed class ExtendedHandshakeMessage : ExtensionMessage
    {
        private static readonly BEncodedString MaxRequestKey = "reqq";
        private static readonly BEncodedString PortKey = "p";
        private static readonly BEncodedString SupportsKey = "m";
        private static readonly BEncodedString VersionKey = "v";
        private static readonly BEncodedString MetadataSizeKey = "metadata_size";

        internal static readonly ExtensionSupport Support = new ExtensionSupport("LT_handshake", 0);

        private int maxRequests;
        private ExtensionSupports supports;
        private string version;
        private int metadataSize;

        public override int ByteLength
        {
            get
            {
                // FIXME Implement this properly
                
                // The length of the payload, 4 byte length prefix, 1 byte BT message id, 1 byte LT message id
                return Create().LengthInBytes() + 4 + 1 + 1;
            }
        }
        
        public int MaxRequests
        {
            get { return maxRequests; }
        }

        public int LocalPort {
            get; private set;
        }

        public ExtensionSupports Supports
        {
            get { return supports; }
        }

        public string Version
        {
            get { return version ?? ""; }
        }

        public int MetadataSize
        {
            get { return metadataSize; }
        }

        #region Constructors
        public ExtendedHandshakeMessage()
            : base(Support.MessageId)
        {
            supports = new ExtensionSupports();
        }

        public ExtendedHandshakeMessage(bool privateTorrent, int metadataSize, int localListenPort)
            : base(Support.MessageId)
        {
            supports = new ExtensionSupports(ExtensionMessage.SupportedMessages);
            if (privateTorrent)
                supports.Remove (PeerExchangeMessage.Support);

            this.metadataSize = metadataSize;
            LocalPort = localListenPort;
        }
        #endregion


        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedValue val;
            BEncodedDictionary d = BEncodedDictionary.Decode<BEncodedDictionary>(buffer, offset, length, false);

            if (d.TryGetValue(MaxRequestKey, out val))
                maxRequests = (int)((BEncodedNumber)val).Number;
            if (d.TryGetValue(VersionKey, out val))
                version = ((BEncodedString)val).Text;
            if (d.TryGetValue(PortKey, out val))
                LocalPort = (int)((BEncodedNumber)val).Number;

            LoadSupports((BEncodedDictionary)d[SupportsKey]);

            if (d.TryGetValue(MetadataSizeKey, out val))
                metadataSize = (int)((BEncodedNumber)val).Number;
        }

        private void LoadSupports(BEncodedDictionary supports)
        {
            ExtensionSupports list = new ExtensionSupports();
            foreach (KeyValuePair<BEncodedString, BEncodedValue> k in supports)
                list.Add(new ExtensionSupport(k.Key.Text, (byte)((BEncodedNumber)k.Value).Number));

            this.supports = list;
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;
            BEncodedDictionary dict = Create();

            written += Write(buffer, written, dict.LengthInBytes() + 1 + 1);
            written += Write(buffer, written, ExtensionMessage.MessageId);
            written += Write(buffer, written, ExtendedHandshakeMessage.Support.MessageId);
            written += dict.Encode(buffer, written);

            CheckWritten(written - offset);
            return written - offset;
        }

        private BEncodedDictionary Create()
        {
            if (!ClientEngine.SupportsExtended)
                throw new MessageException("Libtorrent extension messages not supported");

            BEncodedDictionary mainDict = new BEncodedDictionary();
            BEncodedDictionary supportsDict = new BEncodedDictionary();

            mainDict.Add(MaxRequestKey, (BEncodedNumber)maxRequests);
            mainDict.Add(VersionKey, (BEncodedString)Version);
            mainDict.Add(PortKey, (BEncodedNumber)LocalPort);

            SupportedMessages.ForEach(delegate(ExtensionSupport s) { supportsDict.Add(s.Name, (BEncodedNumber)s.MessageId); });
            mainDict.Add(SupportsKey, supportsDict);

            mainDict.Add(MetadataSizeKey, (BEncodedNumber)metadataSize);

            return mainDict;
        }
        #endregion
    }
}
