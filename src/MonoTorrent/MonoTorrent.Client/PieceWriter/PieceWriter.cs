using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;
using System.IO;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter : IDisposable
    {
        protected PieceWriter()
        {
            
        }

        public abstract bool Exists(TorrentFile file);

        internal bool Exists(TorrentFile[] files)
        {
            Check.Files(files);
            foreach (TorrentFile file in files)
                if (Exists(file))
                    return true;
            return false;
        }

        public abstract void Close(TorrentFile file);

        internal void Close(TorrentFile[] files)
        {
            Check.Files (files);
            foreach (TorrentFile file in files)
                Close(file);
        }

        public virtual void Dispose()
        {

        }

        public abstract void Flush(TorrentFile file);

        internal void Flush(TorrentFile[] files)
        {
            Check.Files(files);
            foreach (TorrentFile file in files)
                Flush(file);
        }

        public abstract void Move(string oldPath, string newPath, bool ignoreExisting);

        internal void Move(string newRoot, TorrentFile[] files, bool ignoreExisting)
        {
            foreach (TorrentFile file in files) {
                string newPath = Path.Combine (newRoot, file.Path);
                Move(file.FullPath, newPath, ignoreExisting);
                file.FullPath = newPath;
            }
        }

        public abstract int Read(BufferedIO data);

        internal int ReadChunk(BufferedIO data)
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