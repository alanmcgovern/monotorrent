using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class ExtendedHandshakeMessage : ExtensionMessage
    {
        private static readonly BEncodedString MaxRequestKey = "reqq";
        private static readonly BEncodedString PortKey = "p";
        private static readonly BEncodedString SupportsKey = "m";
        private static readonly BEncodedString VersionKey = "v";
        private static readonly BEncodedString MetadataSizeKey = "metadata_size";

        internal static readonly ExtensionSupport Support = new ExtensionSupport("LT_handshake", 0);

        private string version;

        public override int ByteLength
        {
            get
            {
                // FIXME Implement this properly

                // The length of the payload, 4 byte length prefix, 1 byte BT message id, 1 byte LT message id
                return Create().LengthInBytes() + 4 + 1 + 1;
            }
        }

        public int MaxRequests { get; private set; }

        public int LocalPort { get; private set; }

        public ExtensionSupports Supports { get; private set; }

        public string Version
        {
            get { return version ?? ""; }
        }

        public int MetadataSize { get; private set; }

        #region Constructors

        public ExtendedHandshakeMessage()
            : base(Support.MessageId)
        {
            Supports = new ExtensionSupports(SupportedMessages);
        }

        public ExtendedHandshakeMessage(int metadataSize)
            : this()
        {
            MetadataSize = metadataSize;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedValue val;
            var d = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);

            if (d.TryGetValue(MaxRequestKey, out val))
                MaxRequests = (int) ((BEncodedNumber) val).Number;
            if (d.TryGetValue(VersionKey, out val))
                version = ((BEncodedString) val).Text;
            if (d.TryGetValue(PortKey, out val))
                LocalPort = (int) ((BEncodedNumber) val).Number;

            LoadSupports((BEncodedDictionary) d[SupportsKey]);

            if (d.TryGetValue(MetadataSizeKey, out val))
                MetadataSize = (int) ((BEncodedNumber) val).Number;
        }

        private void LoadSupports(BEncodedDictionary supports)
        {
            var list = new ExtensionSupports();
            foreach (var k in supports)
                list.Add(new ExtensionSupport(k.Key.Text, (byte) ((BEncodedNumber) k.Value).Number));

            Supports = list;
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;
            var dict = Create();

            written += Write(buffer, written, dict.LengthInBytes() + 1 + 1);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, Support.MessageId);
            written += dict.Encode(buffer, written);

            CheckWritten(written - offset);
            return written - offset;
        }

        private BEncodedDictionary Create()
        {
            if (!ClientEngine.SupportsExtended)
                throw new MessageException("Libtorrent extension messages not supported");

            var mainDict = new BEncodedDictionary();
            var supportsDict = new BEncodedDictionary();

            mainDict.Add(MaxRequestKey, (BEncodedNumber) MaxRequests);
            mainDict.Add(VersionKey, (BEncodedString) Version);
            mainDict.Add(PortKey, (BEncodedNumber) LocalPort);

            SupportedMessages.ForEach(
                delegate(ExtensionSupport s) { supportsDict.Add(s.Name, (BEncodedNumber) s.MessageId); });
            mainDict.Add(SupportsKey, supportsDict);

            mainDict.Add(MetadataSizeKey, (BEncodedNumber) MetadataSize);

            return mainDict;
        }

        #endregion
    }
}