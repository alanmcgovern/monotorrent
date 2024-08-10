//
// HandshakeMessage.cs
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


using System;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages.Peer
{
    /// <summary>
    /// 
    /// </summary>
    public class HandshakeMessage : PeerMessage
    {
        public static readonly int HandshakeLength = Constants.HandshakeLengthV100;

        const byte ExtendedMessagingFlag = 0b00010000;
        const byte FastPeersFlag = 0b00000100;
        const byte UpgradeToV2Flag = 0b00010000;

        public override int ByteLength => Constants.HandshakeLengthV100;

        /// <summary>
        /// The length of the protocol string
        /// </summary>
        public int ProtocolStringLength { get; private set; }


        /// <summary>
        /// The protocol string to send
        /// </summary>
        public string ProtocolString { get; private set; }

        /// <summary>
        /// The infohash of the torrent.
        /// </summary>
        public InfoHash InfoHash { get; private set; }

        /// <summary>
        /// The ID of the peer (20 bytes).
        /// </summary>
        public BEncodedString PeerId { get; private set; }

        /// <summary>
        /// True if the peer supports LibTorrent extended messaging.
        /// </summary>
        public bool SupportsExtendedMessaging { get; private set; }

        /// <summary>
        /// True if the peer supports the Bittorrent FastPeerExtensions.
        /// </summary>
        public bool SupportsFastPeer { get; private set; }

        /// <summary>
        /// True if the infohash sent in the handshake is a V1 infohash, and is from a hybrid torrent.
        /// If the receiving client responds with the corresponding V2 infohash, the connection is treated
        /// as a BitTorrent V2 (BEP52) compatible connection.
        /// </summary>
        public bool SupportsUpgradeToV2 { get; private set; }

        #region Constructors

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString)
            : this (infoHash, peerId, protocolString, true, true)
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString, bool enableFastPeer)
            : this (infoHash, peerId, protocolString, enableFastPeer, true)
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString, bool enableFastPeer, bool enableExtended)
            : this (infoHash, peerId, protocolString, enableFastPeer, enableExtended, false)
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString, bool enableFastPeer, bool enableExtended, bool supportsUpgradeToV2)
        {
            InfoHash = infoHash;
            PeerId = peerId;
            ProtocolString = protocolString;
            ProtocolStringLength = protocolString.Length;
            SupportsFastPeer = enableFastPeer;
            SupportsExtendedMessaging = enableExtended;
            SupportsUpgradeToV2 = supportsUpgradeToV2;
        }
        #endregion


        #region Methods
        public override int Encode (Span<byte> buffer)
        {
            if (ProtocolString is null || InfoHash is null || PeerId is null)
                throw new InvalidOperationException ();

            int written = buffer.Length;

            Write (ref buffer, (byte) ProtocolString.Length);
            WriteAscii (ref buffer, ProtocolString);

            Span<byte> supports = stackalloc byte[8];
            supports.Clear ();

            if (SupportsExtendedMessaging)
                supports[5] |= ExtendedMessagingFlag;
            if (SupportsFastPeer)
                supports[7] |= FastPeersFlag;
            if (SupportsUpgradeToV2)
                supports[7] |= UpgradeToV2Flag;
            supports.CopyTo (buffer);
            buffer = buffer.Slice (supports.Length);

            Write (ref buffer, InfoHash.Truncate ().Span);
            Write (ref buffer, PeerId.Span);

            return written - buffer.Length;
        }

        public HandshakeMessage (ReadOnlySpan<byte> buffer)
        {
            Decode (buffer);
            if (InfoHash is null || PeerId is null || ProtocolString is null)
                throw new InvalidOperationException ();
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            ProtocolStringLength = ReadByte (ref buffer);                  // First byte is length

            // #warning Fix this hack - is there a better way of verifying the protocol string? Hack
            if (ProtocolStringLength != Constants.ProtocolStringV100.Length)
                ProtocolStringLength = Constants.ProtocolStringV100.Length;

            ProtocolString = ReadString (ref buffer, ProtocolStringLength);
            CheckForSupports (ref buffer);
            InfoHash = InfoHash.FromMemory (ReadBytes (ref buffer, 20));
            PeerId = BEncodedString.FromMemory (ReadBytes (ref buffer, 20));
        }

        void CheckForSupports (ref ReadOnlySpan<byte> buffer)
        {
            // There are 8 reserved bytes in total.
            var reservedBytes = buffer.Slice (0, 8);
            buffer = buffer.Slice (8);

            // The bit selected for the extension protocol is bit 20 from the right (counting starts at 0).
            // So (reserved_byte[5] & 0x10) is the expression to use for checking if the client supports extended messaging.
            SupportsExtendedMessaging = (ExtendedMessagingFlag & reservedBytes[5]) != 0;

            // These are enabled by setting the third least significant bit of the last reserved byte in the BitTorrent handshake
            SupportsFastPeer = (FastPeersFlag & reservedBytes[7]) != 0;

            // When initiating a connection and sending the sha1 infohash of such a hybrid torrent a peer can set the 4th most
            // significant bit in the last byte of the reserved bitfield to indicate that it also supports the new format.
            SupportsUpgradeToV2 = (UpgradeToV2Flag & reservedBytes[7]) != 0;
        }

        #endregion


        #region Overridden Methods
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString ()
        {
            var sb = new System.Text.StringBuilder ();
            sb.Append ("HandshakeMessage ");
            sb.Append (" PeerID ");
            sb.Append (PeerId?.Text ?? "");
            sb.Append (" FastPeer ");
            sb.Append (SupportsFastPeer);
            return sb.ToString ();
        }


        public override bool Equals (object? obj)
        {
            if (!(obj is HandshakeMessage msg))
                return false;

            if (InfoHash != msg.InfoHash)
                return false;

            return (PeerId is null ? msg.PeerId is null : PeerId.Equals (msg.PeerId))
                && ProtocolString == msg.ProtocolString
                && SupportsFastPeer == msg.SupportsFastPeer;
        }

        public override int GetHashCode ()
            => InfoHash == null ? 0 : InfoHash.GetHashCode ();

        #endregion
    }
}
