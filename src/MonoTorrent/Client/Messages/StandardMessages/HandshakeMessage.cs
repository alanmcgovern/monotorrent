using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// </summary>
    public class HandshakeMessage : PeerMessage
    {
        internal const int HandshakeLength = 68;
        private const byte ExtendedMessagingFlag = 0x10;
        private const byte FastPeersFlag = 0x04;
        private static readonly byte[] ZeroedBits = new byte[8];

        #region Member Variables

        /// <summary>
        ///     The length of the protocol string
        /// </summary>
        public int ProtocolStringLength { get; private set; }


        /// <summary>
        ///     The protocol string to send
        /// </summary>
        public string ProtocolString { get; private set; }


        /// <summary>
        ///     The infohash of the torrent
        /// </summary>
        public InfoHash InfoHash
        {
            get { return infoHash; }
        }

        internal InfoHash infoHash;


        /// <summary>
        ///     The ID of the peer
        /// </summary>
        public string PeerId { get; private set; }

        public bool SupportsExtendedMessaging { get; private set; }

        /// <summary>
        ///     True if the peer supports the Bittorrent FastPeerExtensions
        /// </summary>
        public bool SupportsFastPeer { get; private set; }

        #endregion

        #region Constructors

        public HandshakeMessage()
            : this(ClientEngine.SupportsFastPeer)
        {
        }

        /// <summary>
        ///     Creates a new HandshakeMessage
        /// </summary>
        public HandshakeMessage(bool enableFastPeer)
            : this(new InfoHash(new byte[20]), "", VersionInfo.ProtocolStringV100, enableFastPeer)
        {
        }

        public HandshakeMessage(InfoHash infoHash, string peerId, string protocolString)
            : this(infoHash, peerId, protocolString, ClientEngine.SupportsFastPeer, ClientEngine.SupportsExtended)
        {
        }

        public HandshakeMessage(InfoHash infoHash, string peerId, string protocolString, bool enableFastPeer)
            : this(infoHash, peerId, protocolString, enableFastPeer, ClientEngine.SupportsExtended)
        {
        }

        public HandshakeMessage(InfoHash infoHash, string peerId, string protocolString, bool enableFastPeer,
            bool enableExtended)
        {
            if (!ClientEngine.SupportsFastPeer && enableFastPeer)
                throw new ProtocolException("The engine does not support fast peer, but fast peer was requested");

            if (!ClientEngine.SupportsExtended && enableExtended)
                throw new ProtocolException("The engine does not support extended, but extended was requested");

            this.infoHash = infoHash;
            PeerId = peerId;
            ProtocolString = protocolString;
            ProtocolStringLength = protocolString.Length;
            SupportsFastPeer = enableFastPeer;
            SupportsExtendedMessaging = enableExtended;
        }

        #endregion

        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, (byte) ProtocolString.Length);
            written += WriteAscii(buffer, written, ProtocolString);
            written += Write(buffer, written, ZeroedBits);

            if (SupportsExtendedMessaging)
                buffer[written - 3] |= ExtendedMessagingFlag;
            if (SupportsFastPeer)
                buffer[written - 1] |= FastPeersFlag;

            written += Write(buffer, written, infoHash.Hash);
            written += WriteAscii(buffer, written, PeerId);

            return CheckWritten(written - offset);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            ProtocolStringLength = ReadByte(buffer, ref offset); // First byte is length

            // #warning Fix this hack - is there a better way of verifying the protocol string? Hack
            if (ProtocolStringLength != VersionInfo.ProtocolStringV100.Length)
                ProtocolStringLength = VersionInfo.ProtocolStringV100.Length;

            ProtocolString = ReadString(buffer, ref offset, ProtocolStringLength);
            CheckForSupports(buffer, ref offset);
            infoHash = new InfoHash(ReadBytes(buffer, ref offset, 20));
            PeerId = ReadString(buffer, ref offset, 20);
        }

        private void CheckForSupports(byte[] buffer, ref int offset)
        {
            // Increment offset first so that the indices are consistent between Encoding and Decoding
            offset += 8;
            SupportsExtendedMessaging = (ExtendedMessagingFlag & buffer[offset - 3]) != 0;
            SupportsFastPeer = (FastPeersFlag & buffer[offset - 1]) != 0;
        }

        public override int ByteLength
        {
            get { return 68; }
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("HandshakeMessage ");
            sb.Append(" PeerID ");
            sb.Append(PeerId);
            sb.Append(" FastPeer ");
            sb.Append(SupportsFastPeer);
            return sb.ToString();
        }


        public override bool Equals(object obj)
        {
            var msg = obj as HandshakeMessage;

            if (msg == null)
                return false;

            if (infoHash != msg.infoHash)
                return false;

            return PeerId == msg.PeerId
                   && ProtocolString == msg.ProtocolString
                   && SupportsFastPeer == msg.SupportsFastPeer;
        }


        public override int GetHashCode()
        {
            return infoHash.GetHashCode() ^ PeerId.GetHashCode() ^ ProtocolString.GetHashCode();
        }

        #endregion
    }
}