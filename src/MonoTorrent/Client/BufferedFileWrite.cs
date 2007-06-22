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
        public PeerIdInternal Id;
        public IPeerMessageInternal Message;
        public Piece Piece;
        public BitField BitField;

		#endregion Fields


		#region Constructors

        public BufferedFileWrite(PeerIdInternal id, ArraySegment<byte> buffer, IPeerMessageInternal message, Piece piece, BitField bitfield)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Message = message;
            this.Piece = piece;
            this.BitField = bitfield;
        }
        
		#endregion Constructors
    }
}
