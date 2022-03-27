//
// ErrorMessage.cs
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
    sealed class ErrorMessage : DhtMessage
    {
        static readonly BEncodedString ErrorListKey = new BEncodedString ("e");
        internal static readonly BEncodedString ErrorType = new BEncodedString ("e");

        internal override NodeId Id => NodeId.Minimum;
        BEncodedList ErrorList => (BEncodedList) properties[ErrorListKey];

        ErrorCode ErrorCode => ((ErrorCode) ((BEncodedNumber) ErrorList[0]).Number);

        string Message => ((BEncodedString) ErrorList[1]).Text;

        public ErrorMessage (BEncodedValue transactionId, ErrorCode error, string message)
            : base (ErrorType)
        {
            var l = new BEncodedList {
                new BEncodedNumber ((int) error),
                new BEncodedString (message)
            };
            properties.Add (ErrorListKey, l);

            TransactionId = transactionId;
        }

        public ErrorMessage (BEncodedDictionary d)
            : base (d)
        {

        }

        public override void Handle (DhtEngine engine, Node node)
        {
            base.Handle (engine, node);

            throw new MessageException (ErrorCode, Message);
        }
    }
}
