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
            // Copy the inital buffer, offset and count so the values won't
            // be lost when doing the reading.
            ArraySegment<byte> orig = data.buffer;
            long origOffset = data.Offset;
            int origCount = data.Count;

            int read = 0;
            int totalRead = 0;

            // Read the data in chunks. For every chunk we read,
            // advance the offset and subtract from the count. This
            // way we can keep filling in the buffer correctly.
            while (totalRead != data.Count)
            {
                read = Read(data);
                data.buffer = new ArraySegment<byte>(data.buffer.Array, data.buffer.Offset + read, data.buffer.Count - read);
                data.Offset += read;
                data.Count -= read;
                totalRead += read;

                if (read == 0 || data.Count == 0)
                    break;
            }

            // Restore the original values so the object remains unchanged
            // as compared to when the user passed it in.
            data.buffer = orig;
            data.Offset = origOffset;
            data.Count = origCount;
            data.ActualCount = totalRead;
            return totalRead;
        }

        public abstract void Write(BufferedIO data);
    }
}