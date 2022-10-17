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

namespace MonoTorrent.Messages.Peer
{
    public class MessageBundle : PeerMessage, IRentable
    {
        List<PeerMessage> Messages { get; }
        public List<Releaser> Releasers { get; }

        public override int ByteLength {
            get {
                int total = 0;
                for (int i = 0; i < Messages.Count; i++)
                    total += Messages[i].ByteLength;
                return total;
            }
        }

        public MessageBundle ()
        {
            Messages = new List<PeerMessage> ();
            Releasers = new List<Releaser> ();
        }

        public void Add (PeerMessage message, Releaser releaser)
        {
            Messages.Add (message);
            Releasers.Add (releaser);
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException ();
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            for (int i = 0; i < Messages.Count; i++)
                buffer = buffer.Slice (Messages[i].Encode (buffer));

            return written - buffer.Length;
        }

        protected override void Reset ()
        {
            base.Reset ();
            foreach (var releaser in Releasers)
                releaser.Dispose ();
            Messages.Clear ();
            Releasers.Clear ();
        }
    }
}
