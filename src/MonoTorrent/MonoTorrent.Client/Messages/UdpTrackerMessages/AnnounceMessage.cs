using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AnnounceMessage : UdpTrackerMessage
    {
        private long connectionId;
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

        public override int ByteLength
        {
            get { return 8 + 4 + 4 + 20 + 20 + 8 + 8 + 8 + 4 + 4 + 4 + 4 + 2; }
        }

        public AnnounceMessage()
        {
            Action = 1;
        }

        public AnnounceMessage(long connectionId, AnnounceParameters parameters)
        {
            Action = 1;
            this.connectionId = connectionId;
            this.downloaded = parameters.BytesDownloaded;
            this.infoHash = parameters.Infohash;
            this.ip = 0;
            this.key = (uint)DateTime.Now.GetHashCode(); // FIXME: Don't do this! It should be constant
            this.left = parameters.BytesLeft;
            this.numWanted = 50;
            this.peerId = parameters.PeerId;
            this.port = (ushort)parameters.Port;
            this.torrentEvent = parameters.ClientEvent;
            this.TransactionId = DateTime.Now.GetHashCode();
            this.uploaded = parameters.BytesUploaded;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, ref offset);
            Action = ReadInt(buffer, ref offset);
            TransactionId = ReadInt(buffer, ref offset);
            infoHash = ReadBytes(buffer, ref offset, 20);
            peerId = ReadString(buffer, ref offset, 20);
            downloaded = ReadLong(buffer, ref offset);
            left = ReadLong(buffer, ref offset);
            uploaded = ReadLong(buffer, ref offset);
            torrentEvent = (TorrentEvent)ReadInt(buffer, ref offset);
            ip = (uint)ReadInt(buffer, ref offset);
            key = (uint)ReadInt(buffer, ref offset);
            numWanted = ReadInt(buffer, ref offset);
            port = (ushort)ReadShort(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int origOffset = offset;
            offset += Write(buffer, offset, connectionId);
            offset += Write(buffer, offset, Action);
            offset += Write(buffer, offset, TransactionId);
            offset += Write(buffer, offset, infoHash, 0, infoHash.Length);
            byte[] b = new byte[20];
            Encoding.ASCII.GetBytes(peerId, 0, peerId.Length, b, 0);
            offset += Write(buffer, offset, b, 0, b.Length);
            offset += Write(buffer, offset, downloaded);
            offset += Write(buffer, offset, left);
            offset += Write(buffer, offset, uploaded);
            offset += Write(buffer, offset, (int)torrentEvent);
            offset += Write(buffer, offset, ip);
            offset += Write(buffer, offset, key);
            offset += Write(buffer, offset, numWanted);
            offset += Write(buffer, offset, port);

            return offset - origOffset;
        }
    }
}
