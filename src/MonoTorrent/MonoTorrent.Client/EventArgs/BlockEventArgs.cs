using System;
using System.Text;
using MonoTorrent.Common;

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
        public PeerId ID
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
        internal BlockEventArgs(TorrentManager manager, Block block, Piece piece, PeerId id)
            : base(manager)
        {
            Init(block, piece, id);
        }

        internal BlockEventArgs(PieceData data)
            : base(data.Id.TorrentManager)
        {
            Block b = data.Piece[PiecePickerBase.GetBlockIndex(data.Piece.Blocks, data.StartOffset, data.Count)];
            Init(b, data.Piece, data.Id.PublicId);
        }
        internal BlockEventArgs(TorrentManager manager, Block block, Piece piece, PeerIdInternal id)
            : base(manager)
        {
            Init(block, piece, id.PublicId);
        }

        private void Init(Block block, Piece piece, PeerId id)
        {
            this.block = block;
            this.id = id;
            this.piece = piece;
        }

        #endregion


        #region Methods

        public override bool Equals(object obj)
        {
            BlockEventArgs args = obj as BlockEventArgs;
            return (args == null) ? false : this.piece.Equals(args.piece)
                                         && this.id.Equals(args.id)
                                         && this.block.Equals(args.block);
        }

        public override int GetHashCode()
        {
            return this.block.GetHashCode();
        }

        #endregion Methods
    }
}