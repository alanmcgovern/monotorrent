//
// UdpTrackerMessage.cs
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
using System.Net.Sockets;

namespace MonoTorrent.Messages.UdpTracker
{
    public abstract class UdpTrackerMessage : Message
    {
        public int Action { get; }
        public int TransactionId { get; protected set; }

        protected UdpTrackerMessage (int action, int transactionId)
        {
            Action = action;
            TransactionId = transactionId;
        }

        public static UdpTrackerMessage DecodeMessage (ReadOnlySpan<byte> buffer, MessageType type, AddressFamily addressFamily)
        {
            UdpTrackerMessage m;
            var actionBuffer = type == MessageType.Request ? buffer.Slice (8) : buffer;
            int action = ReadInt (ref actionBuffer);
            switch (action) {
                case 0:
                    if (type == MessageType.Request)
                        m = new ConnectMessage ();
                    else
                        m = new ConnectResponseMessage ();
                    break;
                case 1:
                    if (type == MessageType.Request)
                        m = new AnnounceMessage ();
                    else
                        m = new AnnounceResponseMessage (addressFamily);
                    break;
                case 2:
                    if (type == MessageType.Request)
                        m = new ScrapeMessage ();
                    else
                        m = new ScrapeResponseMessage ();
                    break;
                case 3:
                    m = new ErrorMessage ();
                    break;
                default:
                    throw new InvalidOperationException ($"Invalid udp message received: {action}");
            }

            try {
                m.Decode (buffer);
            } catch {
                m = new ErrorMessage (0, "Couldn't decode the tracker response");
            }
            return m;
        }

        protected static void ThrowInvalidActionException ()
        {
            throw new MessageException ("Invalid value for 'Action'");
        }
    }
}
