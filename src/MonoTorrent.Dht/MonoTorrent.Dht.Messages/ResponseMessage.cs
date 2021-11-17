//
// ResponseMessage.cs
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


using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    abstract class ResponseMessage : DhtMessage
    {
        static readonly BEncodedString ReturnValuesKey = "r";
        internal static readonly BEncodedString ResponseType = "r";

        internal override NodeId Id => new NodeId ((BEncodedString) Parameters[IdKey]);

        public BEncodedDictionary Parameters => (BEncodedDictionary) properties[ReturnValuesKey];

        protected ResponseMessage (NodeId id, BEncodedValue transactionId)
            : base (ResponseType)
        {
            properties.Add (ReturnValuesKey, new BEncodedDictionary ());
            Parameters.Add (IdKey, id.BencodedString ());
            TransactionId = transactionId;
        }

        protected ResponseMessage (BEncodedDictionary d)
            : base (d)
        {

        }
    }
}
