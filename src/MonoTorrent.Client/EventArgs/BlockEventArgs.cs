using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public class BlockEventArgs : EventArgs
    {
        #region Private Fields

        private Block block;
        private PeerConnectionID id;
        private Piece piece;

        #endregion


        #region Public Properties

        /// <summary>
        /// The block whose state changed
        /// </summary>
        public Block Block
        {
            get { return this.block; }
        }

        
        /// <summary>
        /// The piece that the block belongs too
        /// </summary>
        public Piece Piece
        {
            get { return this.piece; }
        }
        

        /// <summary>
        /// The peer who the block has been requested off
        /// </summary>
        public PeerConnectionID ID
        {
            get { return this.id; }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Creates a new PeerMessageEventArgs
        /// </summary>
        /// <param name="message">The peer message involved</param>
        /// <param name="direction">The direction of the message</param>
        internal BlockEventArgs(Block block, Piece piece, PeerConnectionID id)
        {
            this.block = block;
            this.id = id;
            this.piece = piece;
        }

        #endregion
    }
}