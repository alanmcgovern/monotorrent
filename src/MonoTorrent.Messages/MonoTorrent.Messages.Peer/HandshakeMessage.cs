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
        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, (byte) ProtocolString.Length);
            WriteAscii (ref buffer, ProtocolString);

            Span<byte> supports = stackalloc byte[8];
            supports.Clear ();

            if (SupportsExtendedMessaging)
                supports[5] |= ExtendedMessagingFlag;
            if (SupportsFastPeer)
                supports[7] |= FastPeersFlag;
            supports.CopyTo (buffer);
            buffer = buffer.Slice (supports.Length);

            Write (ref buffer, InfoHash.Span);
            Write (ref buffer, PeerId.Span);

            return written - buffer.Length;
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
            SupportsExtendedMessaging = (ExtendedMessagingFlag & buffer[5]) != 0;
            SupportsFastPeer = (FastPeersFlag & buffer[7]) != 0;
            buffer = buffer.Slice (8);
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
