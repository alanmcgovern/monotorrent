//
// ScrapeResponseMessage.cs
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
    class ScrapeResponseMessage : UdpTrackerMessage
    {
        private List<ScrapeDetails> scrapes;

        public override int ByteLength
        {
            get { return 8 + (scrapes.Count * 12); }
        }

        public List<ScrapeDetails> Scrapes
        {
            get { return scrapes; }
        }

        public ScrapeResponseMessage()
            : this(0, new List<ScrapeDetails>())
        {

        }

        public ScrapeResponseMessage(int transactionId, List<ScrapeDetails> scrapes)
            :base(2, transactionId)
        {
            this.scrapes = scrapes;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            if (Action != ReadInt(buffer, ref offset))
                ThrowInvalidActionException();
            TransactionId = ReadInt(buffer, ref offset);
            while (offset <= (buffer.Length - 12))
            {
                int seeds = ReadInt(buffer, ref offset);
                int complete = ReadInt(buffer, ref offset);
                int leeches = ReadInt(buffer, ref offset);
                scrapes.Add(new ScrapeDetails(seeds, leeches, complete));
            }
        }

        public override int Encode(byte[] buffer, int offset)
        {
            int written = offset;

            written+=Write(buffer, written, Action);
            written+=Write(buffer, written, TransactionId);
            for(int i=0; i < scrapes.Count; i++)
            {
                written += Write(buffer, written, scrapes[i].Seeds);
                written += Write(buffer, written, scrapes[i].Complete);
                written += Write(buffer, written, scrapes[i].Leeches);
            }
            
            return written - offset;
        }
    }
}
