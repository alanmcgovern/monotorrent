//
// GetPeersResponse.cs
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

using MonoTorrent.BEncoding;

namespace MonoTorrent.Dht.Messages
{
    sealed class GetPeersResponse : ResponseMessage
    {
        static readonly BEncodedString NodesKey = new BEncodedString ("nodes");
        static readonly BEncodedString TokenKey = new BEncodedString ("token");
        static readonly BEncodedString ValuesKey = new BEncodedString ("values");

        public BEncodedValue Token {
            get => Parameters.GetValueOrDefault (TokenKey) ?? throw new InvalidOperationException ("The required parameter 'token' was not set.");
            set => Parameters[TokenKey] = value;
        }

        public BEncodedString? Nodes {
            get => (BEncodedString?) Parameters.GetValueOrDefault (NodesKey);
            set {
                if (Parameters.ContainsKey (ValuesKey))
                    throw new InvalidOperationException ("Already contains the values key");
                if (value is null)
                    Parameters.Remove (NodesKey);
                else
                    Parameters[NodesKey] = value;
            }
        }

        public BEncodedList? Values {
            get => (BEncodedList?) Parameters.GetValueOrDefault (ValuesKey);
            set {
                if (Parameters.ContainsKey (NodesKey))
                    throw new InvalidOperationException ("Already contains the nodes key");
                if (value is null)
                    Parameters.Remove (ValuesKey);
                else
                    Parameters[ValuesKey] = value;
            }
        }

        public GetPeersResponse (NodeId id, BEncodedValue transactionId, BEncodedString token)
            : base (id, transactionId)
        {
            Parameters.Add (TokenKey, token);
        }

        public GetPeersResponse (BEncodedDictionary d)
            : base (d)
        {

        }

        public override void Handle (DhtEngine engine, Node node)
        {
            base.Handle (engine, node);
            node.Token = Token;
        }
    }
}
