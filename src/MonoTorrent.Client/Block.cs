//
// Block.cs
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



using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class Block
    {
        #region Member Variables
        /// <summary>
        /// The index of the piece
        /// </summary>
        public int PieceIndex
        {
            get { return this.pieceIndex; }
        }
        private int pieceIndex;


        /// <summary>
        /// The offset in bytes that this block starts at
        /// </summary>
        public int StartOffset
        {
            get { return this.startOffset; }
        }
        private int startOffset;


        /// <summary>
        /// The length in bytes of this block
        /// </summary>
        public int RequestLength
        {
            get { return this.requestLength; }
        }
        private int requestLength;


        /// <summary>
        /// True if this block has been requested
        /// </summary>
        public bool Requested
        {
            get { return this.requested; }
            set { this.requested = value; }
        }
        private bool requested;


        /// <summary>
        /// True if this piece has been recieved
        /// </summary>
        public bool Recieved
        {
            get { return this.recieved; }
            set { this.recieved = value; }
        }
        private bool recieved;
#endregion


        #region Constructors
        /// <summary>
        /// Creates a new Block
        /// </summary>
        /// <param name="pieceIndex">The index of the piece this block is from</param>
        /// <param name="startOffset">The offset in bytes that this block starts at</param>
        /// <param name="requestLength">The length in bytes of the block</param>
        public Block(int pieceIndex, int startOffset, int requestLength)
        {
            this.pieceIndex = pieceIndex;
            this.startOffset = startOffset;
            this.requestLength = requestLength;
        }
        #endregion


        #region Methods
        /// <summary>
        /// Creates a RequestMessage for this Block
        /// </summary>
        /// <returns></returns>
        public RequestMessage CreateRequest()
        {
            return new RequestMessage(this.pieceIndex, this.startOffset, this.requestLength);
        }
        #endregion
    }
}