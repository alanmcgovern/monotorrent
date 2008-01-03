using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client
{
    internal class BufferedIO
    {
        #region Fields
        
        public ArraySegment<byte> Buffer;
        public PeerIdInternal Id;
        public IPeerMessageInternal Message;
        public Piece Piece;
        public ManualResetEvent WaitHandle;

		#endregion Fields


		#region Constructors

        public BufferedIO(PeerIdInternal id, ArraySegment<byte> buffer, IPeerMessageInternal message, Piece piece)
            : this(id, buffer, message, piece, null)
        {
     
        }

        public BufferedIO(PeerIdInternal id, ArraySegment<byte> buffer, IPeerMessageInternal message, Piece piece, ManualResetEvent handle)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.Message = message;
            this.Piece = piece;
            this.WaitHandle = handle;
        }
        
		#endregion Constructors
    }
}
