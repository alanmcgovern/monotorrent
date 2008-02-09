using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    public class BufferedIO
    {
        #region Fields
        
        public ArraySegment<byte> Buffer;
        internal PeerIdInternal Id;
        public PeerMessage Message;
        public Piece Piece;
        public ManualResetEvent WaitHandle;

		#endregion Fields


		#region Constructors

        internal BufferedIO(PeerIdInternal id, ArraySegment<byte> buffer, PeerMessage message, Piece piece)
            : this(id, buffer, message, piece, null)
        {
     
        }

        internal BufferedIO(PeerIdInternal id, ArraySegment<byte> buffer, PeerMessage message, Piece piece, ManualResetEvent handle)
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
