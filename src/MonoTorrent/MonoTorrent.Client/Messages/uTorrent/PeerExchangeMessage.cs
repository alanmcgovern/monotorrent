using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public class PeerExchangeMessage : ExtensionMessage
    {
        private byte[] ZeroArray = new byte[0];
        public static readonly ExtensionSupport Support = CreateSupport("ut_pex");

        private BEncodedDictionary peerDict;
        private static readonly BEncodedString AddedKey = "added";
        private static readonly BEncodedString AddedDotFKey = "added.f";
        private static readonly BEncodedString DroppedKey = "dropped";        

        public PeerExchangeMessage ()
        {
            peerDict = new BEncodedDictionary();
        }
        //TODO done a new ctor with List<Peer> or uri
        public PeerExchangeMessage(byte[] added, byte[] addedDotF, byte[] dropped)
            : this()
        {
            if (added == null)
                added = ZeroArray;
            if (addedDotF == null)
                addedDotF = ZeroArray;
            if (dropped == null)
                dropped = ZeroArray;

            peerDict[AddedKey] = (BEncodedString)added;
            peerDict[AddedDotFKey] = (BEncodedString)addedDotF;
            peerDict[DroppedKey] = (BEncodedString)dropped;
        }

        public byte[] Added
        {
            set { peerDict[AddedKey] = (BEncodedString)( value ?? ZeroArray); }
            get { return ((BEncodedString)peerDict[AddedKey]).TextBytes; }
        }

        public byte[] AddedDotF
        {
            set { peerDict[AddedDotFKey] = (BEncodedString)(value ?? ZeroArray); }
            get { return ((BEncodedString)peerDict[AddedDotFKey]).TextBytes; }
        }

        public byte[] Dropped
        {
            set { peerDict[DroppedKey] = (BEncodedString)(value ?? ZeroArray); }
            get { return ((BEncodedString)peerDict[DroppedKey]).TextBytes; }
        }

        internal override void Handle(PeerId id)
        {
            id.TorrentManager.AddPeers(Peer.Decode((BEncodedString)peerDict[AddedKey]));
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + peerDict.LengthInBytes(); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            peerDict = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, offset, ByteLength - 4);
            written += Write(buffer, written, PeerMessage.LibTorrentMessageId);
            written += Write(buffer, written, Support.MessageId);
            written += peerDict.Encode(buffer, written);
            
            CheckWritten(written - offset);
            return written - offset; ;
        }


        /// <summary>
        /// Overridden. Displays the number of peers received
        /// </summary>
        /// <returns></returns>
        public override string ToString( )
        {
            BEncodedString added = (BEncodedString)peerDict[AddedKey];
            int numPeers = added.TextBytes.Length / 6;

            return String.Format( "PeerExchangeMessage: {0} peers", numPeers );
        }
    }
}
