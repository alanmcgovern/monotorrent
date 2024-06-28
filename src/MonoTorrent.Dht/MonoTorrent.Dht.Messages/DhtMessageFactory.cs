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
using System.Net.Sockets;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    class DhtMessageFactory
    {
        static readonly BEncodedString QueryNameKey = new BEncodedString ("q");
        static readonly BEncodedString MessageTypeKey = new BEncodedString ("y");
        static readonly BEncodedString TransactionIdKey = new BEncodedString ("t");
        static readonly Dictionary<BEncodedString, Func<AddressFamily, BEncodedDictionary, DhtMessage>> queryDecoders = new Dictionary<BEncodedString, Func<AddressFamily, BEncodedDictionary, DhtMessage>> ();

        readonly Dictionary<BEncodedValue, QueryMessage> messages = new Dictionary<BEncodedValue, QueryMessage> ();

        public AddressFamily AddressFamily { get; }
        public int RegisteredMessages => messages.Count;

        static DhtMessageFactory ()
        {
            queryDecoders.Add (new BEncodedString ("announce_peer"), (family, d) => new AnnouncePeer (family, d));
            queryDecoders.Add (new BEncodedString ("find_node"), (family, d) => new FindNode (family, d));
            queryDecoders.Add (new BEncodedString ("get_peers"), (family, d) => new GetPeers (family, d));
            queryDecoders.Add (new BEncodedString ("ping"), (family, d) => new Ping (family, d));
        }

        public DhtMessageFactory (AddressFamily addressFamily)
            => (AddressFamily) = (addressFamily);

        internal bool IsRegistered (BEncodedValue transactionId)
        {
            return messages.ContainsKey (transactionId);
        }

        public void RegisterSend (QueryMessage message)
        {
            messages.Add (message.TransactionId, message);
        }

        public bool UnregisterSend (QueryMessage message)
        {
            return messages.Remove (message.TransactionId);
        }

        public DhtMessage DecodeMessage (BEncodedDictionary dictionary)
        {
            if (!TryDecodeMessage (dictionary, out DhtMessage? message, out string? error))
                throw new MessageException (ErrorCode.GenericError, error!);

            return message!;
        }

        public bool TryDecodeMessage (BEncodedDictionary dictionary, out DhtMessage? message)
        {
            return TryDecodeMessage (dictionary, out message, out string? error);
        }

        public bool TryDecodeMessage (BEncodedDictionary dictionary, out DhtMessage? message, out string? error)
        {
            message = null;
            error = null;

            if (!dictionary.TryGetValue (MessageTypeKey, out BEncodedValue? messageType)) {
                message = null;
                error = "The BEncodedDictionary did not contain the 'q' key, so the message type could not be identified";
                return false;
            }

            if (messageType.Equals (QueryMessage.QueryType)) {
                message = queryDecoders[(BEncodedString) dictionary[QueryNameKey]] (AddressFamily, dictionary);
            } else if (messageType.Equals (ErrorMessage.ErrorType)) {
                message = new ErrorMessage (AddressFamily, dictionary);
                messages.Remove (message.TransactionId!);
            } else {
                var key = (BEncodedString) dictionary[TransactionIdKey];
                if (messages.TryGetValue (key, out QueryMessage? query)) {
                    messages.Remove (key);
                    try {
                        message = query.CreateResponse (dictionary);
                    } catch {
                        error = "Response dictionary was invalid";
                    }
                } else {
                    error = "Response had bad transaction ID";
                }
            }

            // If the transaction ID is null, or invalid, we should bail out
            if (message != null && message.TransactionId == null)
                error = "Response had a null transaction ID";

            // If the node ID is null, or invalid, we should bail out
            if (message != null && message.Id == null)
                error = "Response had a null node ID";

            return error == null && message != null;
        }
    }
}
