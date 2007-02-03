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


        public BufferedFileWrite(PeerConnectionID id, byte[] buffer, IPeerMessageInternal message, Piece piece, BitField bitfield)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Message = message;
            this.Piece = piece;
            this.BitField = bitfield;
        }
    }
}
