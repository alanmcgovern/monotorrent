namespace MonoTorrent
{
    public class Constants
    {
        /// <summary>
        /// Blocks are always 16kB
        /// </summary>
        public const int BlockSize = 16 * 1024;

        /// <summary>
        /// Maximum supported piece length. Value is in bytes.
        /// </summary>
        public static readonly int MaximumPieceLength = 128 * 1024 * 1024;

        /// <summary>
        /// The default maximum concurrent requests which can be made to a single peer.
        /// </summary>
        public const int DefaultMaxPendingRequests = 256;

        /// <summary>
        /// The length of the initial handshake message, in bytes, for a V1 connection
        /// </summary>
        public const int HandshakeLengthV100 = 68;

        /// <summary>
        /// Protocol string for version 1.0 of Bittorrent Protocol
        /// </summary>
        public const string ProtocolStringV100 = "BitTorrent protocol";
    }
}
