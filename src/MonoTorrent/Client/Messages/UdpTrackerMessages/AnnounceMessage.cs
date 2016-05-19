using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class AnnounceMessage : UdpTrackerMessage
    {
        private long connectionId;
        private InfoHash infoHash;  // 20
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

        public long ConnectionId
        {
            get { return connectionId; }
        }

        public long Downloaded
        {
            get { return downloaded; }
            set { downloaded = value; }
        }

        public InfoHash Infohash
        {
            get { return infoHash; }
            set { infoHash = value; }
        }

        public uint Ip
        {
            get { return ip; }
            set { ip = value; }
        }

        public uint Key
        {
            get { return key; }
            set { key = value; }
        }

        public long Left
        {
            get { return left; }
            set { left = value; }
        }

        public int NumWanted
        {
            get { return numWanted; }
            set { numWanted = value; }
        }

        public string PeerId
        {
            get { return peerId; }
            set { peerId = value; }
        }

        public ushort Port
        {
            get { return port; }
            set { port = value; }
        }

        public TorrentEvent TorrentEvent
        {
            get { return torrentEvent; }
            set { torrentEvent = value; }
        }

        public long Uploaded
        {
            get { return uploaded; }
            set { uploaded = value; }
        }

        public AnnounceMessage()
            : this(0, 0, null)
        {

        }

        public AnnounceMessage(int transactionId, long connectionId, AnnounceParameters parameters)
            :base(1, transactionId)
        {
            this.connectionId = connectionId;
            if (parameters == null)
                return;

            this.downloaded = parameters.BytesDownloaded;
            this.infoHash = parameters.InfoHash;
            this.ip = 0;
            this.key = (uint)DateTime.Now.GetHashCode(); // FIXME: Don't do this! It should be constant
            this.left = parameters.BytesLeft;
            this.numWanted = 50;
            this.peerId = parameters.PeerId;
            this.port = (ushort)parameters.Port;
            this.torrentEvent = parameters.ClientEvent;
            this.uploaded = parameters.BytesUploaded;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, ref offset);
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            infoHash = new InfoHash(ReadBytes(buffer, ref offset, 20));
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
            int written = offset;

            written += Write(buffer, written, connectionId);
            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, infoHash.Hash, 0, infoHash.Hash.Length);
            written += WriteAscii(buffer, written, peerId);
            written += Write(buffer, written, downloaded);
            written += Write(buffer, written, left);
            written += Write(buffer, written, uploaded);
            written += Write(buffer, written, (int)torrentEvent);
            written += Write(buffer, written, ip);
            written += Write(buffer, written, key);
            written += Write(buffer, written, numWanted);
            written += Write(buffer, written, port);

            return written - offset;
        }
    }
}
