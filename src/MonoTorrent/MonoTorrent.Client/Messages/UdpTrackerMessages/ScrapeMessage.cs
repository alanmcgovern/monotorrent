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



using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class ScrapeMessage : UdpTrackerMessage
    {
        long connectionId;
        List<byte[]> infohashes;

        public override int ByteLength
        {
            get { return 8 + 4 + 4 + infohashes.Count * 20; }
        }

        public List<byte[]> InfoHashes
        {
            get { return infohashes; }
        }

        public ScrapeMessage()
            : this(0, 0, new List<byte[]>())
        {

        }

        public ScrapeMessage(int transactionId, long connectionId, List<byte[]> infohashes)
            : base (2, transactionId)
        {
            this.connectionId = connectionId;
            this.infohashes = infohashes;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            connectionId = ReadLong(buffer, ref offset);
            if (Action != ReadInt(buffer, ref offset))
                throw new MessageException("Udp message decoded incorrectly");
            TransactionId = ReadInt(buffer, ref offset);
            while(offset <= (length - 20))
                infohashes.Add(ReadBytes(buffer, ref offset, 20));
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written += Write(buffer, written, connectionId);
            written += Write(buffer, written, Action);
            written += Write(buffer, written, TransactionId);
            for (int i = 0; i < infohashes.Count; i++)
                written += Write(buffer, written, infohashes[i]);

            return written - offset;
        }
    }
}