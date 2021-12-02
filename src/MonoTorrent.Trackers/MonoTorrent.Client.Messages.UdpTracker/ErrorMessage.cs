//
// ErrorMessage.cs
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
using System.Text;

namespace MonoTorrent.Messages.UdpTracker
{
    public class ErrorMessage : UdpTrackerMessage
    {
        public override int ByteLength => 4 + 4 + Encoding.ASCII.GetByteCount (Error);
        public string Error { get; private set; }

        public ErrorMessage ()
            : this (0, "")
        {
        }

        public ErrorMessage (int transactionId, string error)
            : base (3, transactionId)
        {
            Error = error;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            if (Action != ReadInt (ref buffer))
                ThrowInvalidActionException ();
            TransactionId = ReadInt (ref buffer);
            Error = ReadString (ref buffer, buffer.Length);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, Action);
            Write (ref buffer, TransactionId);
            WriteAscii (ref buffer, Error);

            return written - buffer.Length;
        }
    }
}
