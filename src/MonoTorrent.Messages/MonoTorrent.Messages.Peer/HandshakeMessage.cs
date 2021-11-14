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


using MonoTorrent.BEncoding;

namespace MonoTorrent.Messages.Peer
{
    /// <summary>
    /// 
    /// </summary>
    public class HandshakeMessage : PeerMessage
    {
        public static readonly int HandshakeLength = Constants.HandshakeLengthV100;

        static readonly byte[] ZeroedBits = new byte[8];
        const byte ExtendedMessagingFlag = 0x10;
        const byte FastPeersFlag = 0x04;


        #region Member Variables

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
        #endregion


        #region Constructors
        public HandshakeMessage ()
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString)
            : this (infoHash, peerId, protocolString, true, true)
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString, bool enableFastPeer)
            : this (infoHash, peerId, protocolString, enableFastPeer, true)
        {

        }

        public HandshakeMessage (InfoHash infoHash, BEncodedString peerId, string protocolString, bool enableFastPeer, bool enableExtended)
        {
            InfoHash = infoHash;
            PeerId = peerId;
            ProtocolString = protocolString;
            ProtocolStringLength = protocolString.Length;
            SupportsFastPeer = enableFastPeer;
            SupportsExtendedMessaging = enableExtended;
        }
        #endregion


        #region Methods
        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, (byte) ProtocolString.Length);
            written += WriteAscii (buffer, written, ProtocolString);
            written += Write (buffer, written, ZeroedBits);

            if (SupportsExtendedMessaging)
                buffer[written - 3] |= ExtendedMessagingFlag;
            if (SupportsFastPeer)
                buffer[written - 1] |= FastPeersFlag;

            written += Write (buffer, written, InfoHash.UnsafeAsArray ());
            written += Write (buffer, written, PeerId.TextBytes);

            return written - offset;
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            ProtocolStringLength = ReadByte (buffer, ref offset);                  // First byte is length

            // #warning Fix this hack - is there a better way of verifying the protocol string? Hack
            if (ProtocolStringLength != Constants.ProtocolStringV100.Length)
                ProtocolStringLength = Constants.ProtocolStringV100.Length;

            ProtocolString = ReadString (buffer, ref offset, ProtocolStringLength);
            CheckForSupports (buffer, ref offset);
            InfoHash = new InfoHash (ReadBytes (buffer, ref offset, 20));
            PeerId = ReadBytes (buffer, ref offset, 20);
        }

        void CheckForSupports (byte[] buffer, ref int offset)
        {
            // Increment offset first so that the indices are consistent between Encoding and Decoding
            offset += 8;
            SupportsExtendedMessaging = (ExtendedMessagingFlag & buffer[offset - 3]) != 0;
            SupportsFastPeer = (FastPeersFlag & buffer[offset - 1]) != 0;
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
            sb.Append (PeerId.Text);
            sb.Append (" FastPeer ");
            sb.Append (SupportsFastPeer);
            return sb.ToString ();
        }


        public override bool Equals (object obj)
        {
            if (!(obj is HandshakeMessage msg))
                return false;

            if (InfoHash != msg.InfoHash)
                return false;

            return PeerId.Equals (msg.PeerId)
                && ProtocolString == msg.ProtocolString
                && SupportsFastPeer == msg.SupportsFastPeer;
        }

        public override int GetHashCode ()
        {
            return (InfoHash.GetHashCode () ^ PeerId.GetHashCode () ^ ProtocolString.GetHashCode ());
        }
        #endregion
    }
}
