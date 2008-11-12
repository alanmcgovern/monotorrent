//
// MessageFactory.cs
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
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    delegate Message Creator(BEncodedDictionary dictionary);
    delegate Message ResponseCreator(BEncodedDictionary dictionary, QueryMessage message);
    
    internal static class MessageFactory
    {
        private static readonly string QueryNameKey = "q";
        private static BEncodedString MessageTypeKey = "y";
        private static BEncodedString TransactionIdKey = "t";

        public static int RegisteredMessages
        {
            get { return messages.Count; }
        }

        static MessageFactory()
        {
            queryDecoders.Add("announce_peer", delegate(BEncodedDictionary d) { return new AnnouncePeer(d); });
            queryDecoders.Add("find_node",     delegate(BEncodedDictionary d) { return new FindNode(d); });
            queryDecoders.Add("get_peers",     delegate(BEncodedDictionary d) { return new GetPeers(d); });
            queryDecoders.Add("ping",          delegate(BEncodedDictionary d) { return new Ping(d); });
        }

        private static Dictionary<BEncodedString, QueryMessage> messages = new Dictionary<BEncodedString, QueryMessage>();
        private static Dictionary<BEncodedString, Creator> queryDecoders = new Dictionary<BEncodedString, Creator>();

        internal static bool IsRegistered(BEncodedString transactionId)
        {
            return messages.ContainsKey(transactionId);
        }

        public static void RegisterSend(QueryMessage message)
        {
            messages.Add(message.TransactionId, message);
        }

        public static bool UnregisterSend(QueryMessage message)
        {
            return messages.Remove(message.TransactionId);
        }

        public static Message DecodeMessage(BEncodedDictionary dictionary)
        {
            QueryMessage msg = null;

            if (dictionary[MessageTypeKey].Equals(QueryMessage.QueryType))
            {
                return queryDecoders[(BEncodedString)dictionary[QueryNameKey]](dictionary);
            }
            else if (dictionary[MessageTypeKey].Equals(ErrorMessage.ErrorType))
            {
                return new ErrorMessage(dictionary);
            }
            else
            {
                BEncodedString key = (BEncodedString)dictionary[TransactionIdKey];
                if (!messages.TryGetValue(key, out msg))
                    throw new MessageException(ErrorCode.GenericError, string.Format("{0}: {1}. {2}: {3}",
                        "Response message with bad transaction:", key, "full message:", dictionary));
                messages.Remove(key);
            }

            return msg.ResponseCreator(dictionary, msg);
        }
    }
}
