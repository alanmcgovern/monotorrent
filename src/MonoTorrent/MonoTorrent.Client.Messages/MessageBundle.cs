//
// MessageBundle.cs
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

using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client.Messages
{
    class MessageBundle : PeerMessage
    {
        public List<PeerMessage> Messages { get; }

        public MessageBundle ()
        {
            Messages = new List<PeerMessage> ();
        }

        public MessageBundle (int capacity)
        {
            Messages = new List<PeerMessage> (capacity);
        }

        public MessageBundle (PeerMessage message)
            : this ()
        {
            Messages.Add (message);
        }

        internal MessageBundle (IList<PieceRequest> requests)
            : this ()
        {
            foreach (PieceRequest m in requests)
                Messages.Add (new RequestMessage (m.PieceIndex, m.StartOffset, m.RequestLength));
        }

        public override int ByteLength {
            get {
                int total = 0;
                for (int i = 0; i < Messages.Count; i++)
                    total += Messages[i].ByteLength;
                return total;
            }
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            for (int i = 0; i < Messages.Count; i++)
                written += Messages[i].Encode (buffer, written);

            return CheckWritten (written - offset);
        }
    }
}
