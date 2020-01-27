//
// BlockEventArgs.cs
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


namespace MonoTorrent.Client
{
    public class BlockEventArgs : TorrentEventArgs
    {
        #region Private Fields

        private Block block;
        private PeerId id;
        private Piece piece;

        #endregion


        #region Public Properties

        /// <summary>
        /// The block whose state changed
        /// </summary>
        public Block Block {
            get { return this.block; }
        }


        /// <summary>
        /// The piece that the block belongs too
        /// </summary>
        public Piece Piece {
            get { return this.piece; }
        }


        /// <summary>
        /// The peer who the block has been requested off
        /// </summary>
        public PeerId ID {
            get { return this.id; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new BlockEventArgs
        /// </summary>
        internal BlockEventArgs (TorrentManager manager, Block block, Piece piece, PeerId id)
            : base (manager)
        {
            Init (block, piece, id);
        }

        private void Init (Block block, Piece piece, PeerId id)
        {
            this.block = block;
            this.id = id;
            this.piece = piece;
        }

        #endregion


        #region Methods

        public override bool Equals (object obj)
        {
            return (!(obj is BlockEventArgs args)) ? false : this.piece.Equals (args.piece)
                                                             && this.id.Equals (args.id)
                                                             && this.block.Equals (args.block);
        }

        public override int GetHashCode ()
        {
            return this.block.GetHashCode ();
        }

        #endregion Methods
    }
}