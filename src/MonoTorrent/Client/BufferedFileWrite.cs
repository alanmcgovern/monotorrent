using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class BufferedFileWrite
    {
        #region Fields
        
        public ArraySegment<byte> Buffer;
        public PeerId Id;
        public IPeerMessageInternal Message;
        public Piece Piece;
        public BitField BitField;
        public IntCollection UnhashedPieces;

		#endregion Fields


		#region Constructors

        public BufferedFileWrite(PeerId id, ArraySegment<byte> buffer, IPeerMessageInternal message, Piece piece, BitField bitfield,
            IntCollection unhashedPieces)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Message = message;
            this.Piece = piece;
            this.BitField = bitfield;
            this.UnhashedPieces = unhashedPieces;
        }
        
		#endregion Constructors
    }
}
