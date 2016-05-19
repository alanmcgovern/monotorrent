using System.Collections.Generic;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    internal class ScrapeResponseMessage : UdpTrackerMessage
    {
        public ScrapeResponseMessage()
            : this(0, new List<ScrapeDetails>())
        {
        }

        public ScrapeResponseMessage(int transactionId, List<ScrapeDetails> scrapes)
            : base(2, transactionId)
        {
            Scrapes = scrapes;
        }

        public override int ByteLength
        {
            get { return 8 + Scrapes.Count*12; }
        }

        public List<ScrapeDetails> Scrapes { get; }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            while (offset <= buffer.Length - 12)
            {
                var seeds = ReadInt(buffer, ref offset);
                var complete = ReadInt(buffer, ref offset);
                var leeches = ReadInt(buffer, ref offset);
                Scrapes.Add(new ScrapeDetails(seeds, leeches, complete));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            for (var i = 0; i < Scrapes.Count; i++)
            {
                written += Write(buffer, written, Scrapes[i].Seeds);
                written += Write(buffer, written, Scrapes[i].Complete);
                written += Write(buffer, written, Scrapes[i].Leeches);
            }

            return written - offset;
        }
    }
}