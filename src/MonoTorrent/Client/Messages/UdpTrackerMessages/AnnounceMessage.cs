using System;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    public class AnnounceMessage : UdpTrackerMessage
    {
        public AnnounceMessage()
            : this(0, 0, null)
        {
        }

        public AnnounceMessage(int transactionId, long connectionId, AnnounceParameters parameters)
            : base(1, transactionId)
        {
            ConnectionId = connectionId;
            if (parameters == null)
                return;

            Downloaded = parameters.BytesDownloaded;
            Infohash = parameters.InfoHash;
            Ip = 0;
            Key = (uint) DateTime.Now.GetHashCode(); // FIXME: Don't do this! It should be constant
            Left = parameters.BytesLeft;
            NumWanted = 50;
            PeerId = parameters.PeerId;
            Port = (ushort) parameters.Port;
            TorrentEvent = parameters.ClientEvent;
            Uploaded = parameters.BytesUploaded;
        }

        public override int ByteLength
        {
            get { return 8 + 4 + 4 + 20 + 20 + 8 + 8 + 8 + 4 + 4 + 4 + 4 + 2; }
        }

        public long ConnectionId { get; private set; }

        public long Downloaded { get; set; }

        public InfoHash Infohash { get; set; }

        public uint Ip { get; set; }

        public uint Key { get; set; }

        public long Left { get; set; }

        public int NumWanted { get; set; }

        public string PeerId { get; set; }

        public ushort Port { get; set; }

        public TorrentEvent TorrentEvent { get; set; }

        public long Uploaded { get; set; }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            ConnectionId = ReadLong(buffer, ref offset);
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            Infohash = new InfoHash(ReadBytes(buffer, ref offset, 20));
            PeerId = ReadString(buffer, ref offset, 20);
            Downloaded = ReadLong(buffer, ref offset);
            Left = ReadLong(buffer, ref offset);
            Uploaded = ReadLong(buffer, ref offset);
            TorrentEvent = (TorrentEvent) ReadInt(buffer, ref offset);
            Ip = (uint) ReadInt(buffer, ref offset);
            Key = (uint) ReadInt(buffer, ref offset);
            NumWanted = ReadInt(buffer, ref offset);
            Port = (ushort) ReadShort(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, ConnectionId);
            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            written += Write(buffer, written, Infohash.Hash, 0, Infohash.Hash.Length);
            written += WriteAscii(buffer, written, PeerId);
            written += Write(buffer, written, Downloaded);
            written += Write(buffer, written, Left);
            written += Write(buffer, written, Uploaded);
            written += Write(buffer, written, (int) TorrentEvent);
            written += Write(buffer, written, Ip);
            written += Write(buffer, written, Key);
            written += Write(buffer, written, NumWanted);
            written += Write(buffer, written, Port);

            return written - offset;
        }
    }
}