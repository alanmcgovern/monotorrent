using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    internal class BufferedFileWrite
    {
        public byte[] Buffer;
        public PeerConnectionID Id;
        public IPeerMessageInternal Message;
        public Piece Piece;
        public BitField BitField;
        public IntCollection UnhashedPieces;


        public BufferedFileWrite(PeerConnectionID id, byte[] buffer, IPeerMessageInternal message, Piece piece, BitField bitfield,
            IntCollection unhashedPieces)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Message = message;
            this.Piece = piece;
            this.BitField = bitfield;
            this.UnhashedPieces = unhashedPieces;
        }
    }
}
