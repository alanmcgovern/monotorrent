using System;
using System.Collections.Generic;
using System.IO;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter : IPieceWriter, IDisposable
    {
        public virtual void Dispose()
        {
        }

        public abstract bool Exists(TorrentFile file);

        public abstract void Close(TorrentFile file);

        public abstract void Flush(TorrentFile file);

        public abstract int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count);

        public abstract void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count);

        internal void Close(IList<TorrentFile> files)
        {
            Check.Files(files);
            foreach (var file in files)
                Close(file);
        }

        internal void Flush(IList<TorrentFile> files)
        {
            Check.Files(files);
            foreach (var file in files)
                Flush(file);
        }

        public abstract void Move(string oldPath, string newPath, bool ignoreExisting);

        internal void Move(string newRoot, IList<TorrentFile> files, bool ignoreExisting)
        {
            foreach (var file in files)
            {
                var newPath = Path.Combine(newRoot, file.Path);
                Move(file.FullPath, newPath, ignoreExisting);
                file.FullPath = newPath;
            }
        }

        internal bool ReadBlock(IList<TorrentFile> files, int piece, int blockIndex, byte[] buffer, int bufferOffset,
            int pieceLength, long torrentSize)
        {
            var offset = (long) piece*pieceLength + blockIndex*Piece.BlockSize;
            var count = (int) Math.Min(Piece.BlockSize, torrentSize - offset);

            return Read(files, offset, buffer, bufferOffset, count, pieceLength, torrentSize);
        }

        internal bool Read(IList<TorrentFile> files, long offset, byte[] buffer, int bufferOffset, int count,
            int pieceLength, long torrentSize)
        {
            if (offset < 0 || offset + count > torrentSize)
                throw new ArgumentOutOfRangeException("offset");

            var i = 0;
            var totalRead = 0;

            for (i = 0; i < files.Count; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalRead < count)
            {
                var fileToRead = (int) Math.Min(files[i].Length - offset, count - totalRead);
                fileToRead = Math.Min(fileToRead, Piece.BlockSize);

                if (fileToRead != Read(files[i], offset, buffer, bufferOffset + totalRead, fileToRead))
                    return false;

                offset += fileToRead;
                totalRead += fileToRead;
                if (offset >= files[i].Length)
                {
                    offset = 0;
                    i++;
                }
            }

            //monitor.BytesSent(totalRead, TransferType.Data);
            return true;
        }

        internal void Write(IList<TorrentFile> files, long offset, byte[] buffer, int bufferOffset, int count,
            int pieceLength, long torrentSize)
        {
            if (offset < 0 || offset + count > torrentSize)
                throw new ArgumentOutOfRangeException("offset");

            var i = 0;
            var totalWritten = 0;

            for (i = 0; i < files.Count; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalWritten < count)
            {
                var fileToWrite = (int) Math.Min(files[i].Length - offset, count - totalWritten);
                fileToWrite = Math.Min(fileToWrite, Piece.BlockSize);

                Write(files[i], offset, buffer, bufferOffset + totalWritten, fileToWrite);

                offset += fileToWrite;
                totalWritten += fileToWrite;
                if (offset >= files[i].Length)
                {
                    offset = 0;
                    i++;
                }
            }

            //monitor.BytesSent(totalRead, TransferType.Data);
        }
    }
}