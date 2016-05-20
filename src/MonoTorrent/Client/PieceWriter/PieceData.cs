using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public partial class DiskManager
    {
        public class BufferedIO : ICacheable
        {
            internal byte[] buffer;

            public int ActualCount { get; set; }

            public int BlockIndex
            {
                get { return PieceOffset/Piece.BlockSize; }
            }

            public byte[] Buffer
            {
                get { return buffer; }
            }

            internal DiskIOCallback Callback { get; set; }

            public int Count { get; set; }

            internal PeerId Id { get; set; }

            public int PieceIndex
            {
                get { return (int) (Offset/PieceLength); }
            }

            public int PieceLength { get; private set; }

            public int PieceOffset
            {
                get
                {
                    return (int) (Offset%PieceLength);
                    ;
                }
            }

            public long Offset { get; set; }

            public IList<TorrentFile> Files { get; private set; }

            internal TorrentManager Manager { get; private set; }

            public bool Complete { get; set; }

            public void Initialise()
            {
                Initialise(null, BufferManager.EmptyBuffer, 0, 0, 0, null);
            }

            public void Initialise(TorrentManager manager, byte[] buffer, long offset, int count, int pieceLength,
                IList<TorrentFile> files)
            {
                ActualCount = 0;
                this.buffer = buffer;
                Callback = null;
                Complete = false;
                Count = count;
                Files = files;
                Manager = manager;
                Offset = offset;
                Id = null;
                PieceLength = pieceLength;
            }

            public override string ToString()
            {
                return string.Format("Piece: {0} Block: {1} Count: {2}", PieceIndex, BlockIndex, Count);
            }
        }
    }
}