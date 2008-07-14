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
using System.Collections.Generic;
using System.Text;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Dht;


namespace MonoTorrent.Dht.Messages
{
    internal class ErrorMessage : Message
    {
        private static readonly BEncodedString ErrorListKey = "e";
        internal static readonly BEncodedString ErrorType = "e";
		
        private BEncodedList ErrorList
        {
            get { return (BEncodedList)properties[ErrorListKey]; }
        }
		
		private eErrorCode ErrorCode
        {
            get { return ((eErrorCode)((BEncodedNumber)ErrorList[0]).Number); }
        }
		
		private string Message
        {
            get { return ((BEncodedString)ErrorList[1]).Text; }
        }
		
        public ErrorMessage(eErrorCode error, string message)
            : base(ErrorType)
        {
		    BEncodedList l = new BEncodedList();
		    l.Add(new BEncodedNumber((int)error));
			l.Add(new BEncodedString(message));
            properties.Add(ErrorListKey, l);
        }

        public ErrorMessage(BEncodedDictionary d)
            : base(d)
        {

        }

        public override bool Handle(DhtEngine engine, IPEndPoint source)
        {
            throw new MessageException(ErrorCode, Message);
        }
    }
}
