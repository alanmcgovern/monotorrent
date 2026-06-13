//
// DhtMessageFactory.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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
using System.Diagnostics.CodeAnalysis;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Messages;
using MonoTorrent.Messages;

namespace MonoTorrent.Dht.Messages
{
    class DhtMessageFactory
    {
        readonly Dictionary<(TransactionId, CompactEndPoint), ReadOnlyMemory<byte>> messages = new Dictionary<(TransactionId, CompactEndPoint), ReadOnlyMemory<byte>> ();

        public int RegisteredMessages => messages.Count;

        internal bool IsRegistered (ReadOnlySpan<byte> transactionId, CompactEndPoint endPoint)
        {
            if (transactionId.Length != 2)
                throw new NotSupportedException ();

            return messages.ContainsKey ((TransactionId.From (transactionId), endPoint));
        }

        public void RegisterSend (ReadOnlySpan<byte> transactionId, ReadOnlyMemory<byte> queryMessage, CompactEndPoint endPoint)
        {
            if (transactionId.Length != 2)
                throw new InvalidOperationException ("Transaction ids should be 2 byte BEncodedStrings");

            messages.Add ((TransactionId.From (transactionId), endPoint), queryMessage);
        }

        public bool UnregisterSend (ReadOnlySpan<byte> transactionId, CompactEndPoint endPoint)
        {
            if (transactionId.Length != 2)
                throw new InvalidOperationException ("Transaction ids should be 2 byte BEncodedStrings");

            return messages.Remove ((TransactionId.From (transactionId), endPoint));
        }
    }
}
