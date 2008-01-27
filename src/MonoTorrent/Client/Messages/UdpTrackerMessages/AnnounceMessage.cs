using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Tracker.UdpTrackerMessages
{
    class AnnounceMessage : Message
    {
        private long connectionId;
        private int action;
        private int transactionId;
        private byte[] infoHash;  // 20
        private string peerId; //20
        private long downloaded;
        private long left;
        private long uploaded;
        private TorrentEvent torrentEvent;
        private uint ip;
        private uint key;
        private int numWanted;
        private ushort port;
        private ushort extensions;

        public override int ByteLength
        {
            get { return 8 + 4 + 4 + 20 + 20 + 8 + 8 + 8 + 4 + 4 + 4 + 4 + 2 + 2; }
        }

        public AnnounceMessage()
        {

        }

        public AnnounceMessage(long connectionId, AnnounceParameters parameters)
        {
            this.action = 1;
            this.connectionId = connectionId;
            this.downloaded = parameters.BytesDownloaded;
            this.extensions = 0;
            this.infoHash = parameters.Infohash;
            this.ip = 0;
            this.key = (uint)DateTime.Now.GetHashCode(); // FIXME: Don't do this! It should be constant
            this.left = parameters.BytesLeft;
            this.numWanted = 50;
            this.peerId = parameters.PeerId;
            this.port = (ushort)parameters.Port;
            this.torrentEvent = parameters.ClientEvent;
            this.transactionId = DateTime.Now.GetHashCode();
            this.uploaded = parameters.BytesUploaded;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, offset);
            offset += 8;
            action = ReadInt(buffer, offset + 8);
            offset += 4;
            transactionId = ReadInt(buffer, offset + 12);
            offset += 4;
            infoHash = new byte[20];
            Buffer.BlockCopy(buffer, offset, infoHash, 0, 20);
            offset += 20;
            peerId = Encoding.ASCII.GetString(buffer, offset, 20);
            offset += 20;
            downloaded = ReadLong(buffer, offset);
            offset += 8;
            left = ReadLong(buffer, offset);
            offset += 8;
            uploaded = ReadLong(buffer, offset);
            offset += 8;
            torrentEvent = (TorrentEvent)ReadInt(buffer, offset);
            offset += 4;
            ip = (uint)ReadInt(buffer, offset);
            offset += 4;
            key = (uint)ReadInt(buffer, offset);
            offset += 4;
            numWanted = ReadInt(buffer, offset);
            offset += 4;
            port = (ushort)ReadShort(buffer, offset);
            offset += 2;
            extensions = (ushort)ReadShort(buffer, offset);
            offset += 2;
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int origOffset = offset;
            offset += Write(buffer, offset, connectionId);
            offset += Write(buffer, offset, action);
            offset += Write(buffer, offset, transactionId);
            offset += Write(buffer, offset, infoHash);
            byte[] b = new byte[20];
            Encoding.ASCII.GetBytes(peerId, 0, peerId.Length, b, 0);
            offset += Write(buffer, offset, b);
            offset += Write(buffer, offset, downloaded);
            offset += Write(buffer, offset, left);
            offset += Write(buffer, offset, uploaded);
            offset += Write(buffer, offset, (int)torrentEvent);
            offset += Write(buffer, offset, ip);
            offset += Write(buffer, offset, key);
            offset += Write(buffer, offset, numWanted);
            offset += Write(buffer, offset, port);
            offset += Write(buffer, offset, extensions);

            return offset - origOffset;
        }
    }
}
