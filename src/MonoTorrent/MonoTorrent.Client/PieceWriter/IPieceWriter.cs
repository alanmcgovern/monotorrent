using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter : IDisposable
    {
        protected PieceWriter()
        {
            //pressures = new List<Pressure>();
        }

        private IEnumerable<int> AllBlocks(TorrentManager manager)
        {
            for (int i = 0; i < manager.Torrent.PieceLength / Piece.BlockSize; i++)
                yield return i;
        }

        public abstract void Close(string path, TorrentFile[] files);

        public virtual void Dispose()
        {

        }

        public abstract void Flush(string path, TorrentFile[] files);

        public abstract void Flush(string path, TorrentFile[] files, int pieceIndex);

        public abstract int Read(BufferedIO data);

        public int ReadChunk(BufferedIO data)
        {
            BufferedIO clone = (BufferedIO)((ICloneable)data).Clone();
            int read = 0;
            int totalRead = 0;

            while (totalRead != data.Count)
            {
                read = Read(clone);
                clone.buffer = new ArraySegment<byte>(clone.buffer.Array, clone.buffer.Offset + read, clone.buffer.Count - read);
                clone.PieceOffset += read;
                clone.Count -= read;
                totalRead += read;

                if (read == 0 || totalRead == data.Count)
                    break;
            }

            data.ActualCount = totalRead;
            return totalRead;
        }

        public abstract void Write(BufferedIO data);
    }
}