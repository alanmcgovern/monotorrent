//
// ScrapeMessage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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


using System.Collections.Generic;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ScrapeMessage : UdpTrackerMessage
    {
        public override int ByteLength => 8 + 4 + 4 + InfoHashes.Count * 20;
        long ConnectionId { get; set; }
        public List<byte[]> InfoHashes { get; }

        public ScrapeMessage ()
            : this (0, 0, new List<byte[]> ())
        {

        }

        public ScrapeMessage (int transactionId, long connectionId, List<byte[]> infohashes)
            : base (2, transactionId)
        {
            ConnectionId = connectionId;
            InfoHashes = infohashes;
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            ConnectionId = ReadLong (buffer, ref offset);
            if (Action != ReadInt (buffer, ref offset))
                throw new MessageException ("Udp message decoded incorrectly");
            TransactionId = ReadInt (buffer, ref offset);
            while (offset <= (length - 20))
                InfoHashes.Add (ReadBytes (buffer, ref offset, 20));
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, ConnectionId);
            written += Write (buffer, written, Action);
            written += Write (buffer, written, TransactionId);
            for (int i = 0; i < InfoHashes.Count; i++)
                written += Write (buffer, written, InfoHashes[i]);

            return written - offset;
        }
    }
}