//
// UnchokeMessage.sc
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Net;

namespace MonoTorrent.Client.Messages.Standard
{
    public class UnchokeMessage : PeerMessage
    {
        internal static readonly byte MessageId = 1;
        private const int messageLength = 1;


        #region Constructors
        
        public UnchokeMessage()
        {
        }

        #endregion


        #region Methods

        public override int Encode(byte[] buffer, int offset)
        {
			int written = offset;

			written += Write(buffer, written, messageLength);
			written += Write(buffer, written, MessageId);

            CheckWritten(written - offset);
            return written - offset;
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            // No decoding needed
        }

        internal override void Handle(PeerId id)
        {
            id.IsChoking = false;

            // Add requests to the peers message queue
            while (id.TorrentManager.PieceManager.AddPieceRequest(id)) { }
        }

        public override int ByteLength
        {
            get { return (messageLength + 4); }
        }
        #endregion


        #region Overridden Methods
        
        public override string ToString()
        {
            return "UnChokeMessage";
        }

        public override bool Equals(object obj)
        {
            return (obj is UnchokeMessage);
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        #endregion
    }
}