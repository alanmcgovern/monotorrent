//
// PieceWriter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.Threading;
using System.IO;

namespace MonoTorrent.Client.PieceWriters
{
    public abstract class PieceWriter : IPieceWriter, IDisposable
    {
        protected PieceWriter()
        {
            
        }

        public abstract bool Exists(TorrentFile file);

        public abstract void Close(TorrentFile file);

        internal void Close(IList<TorrentFile> files)
        {
            Check.Files (files);
            foreach (TorrentFile file in files)
                Close(file);
        }

        public virtual void Dispose()
        {

        }

        public abstract void Flush(TorrentFile file);

        internal void Flush(IList<TorrentFile> files)
        {
            Check.Files(files);
            foreach (TorrentFile file in files)
                Flush(file);
        }

        public abstract void Move(TorrentFile file, string newPath, bool overwrite);

        internal void Move(string newRoot, IList<TorrentFile> files, bool overwrite)
        {
            foreach (TorrentFile file in files) {
                string newPath = Path.Combine (newRoot, file.Path);
                Move(file, newPath, overwrite);
                file.FullPath = newPath;
            }
        }

        internal bool ReadBlock(IList<TorrentFile> files, int piece, int blockIndex, byte[] buffer, int bufferOffset, int pieceLength, long torrentSize)
        {
            long offset = (long) piece * pieceLength + blockIndex * Piece.BlockSize;
            int count = (int) Math.Min (Piece.BlockSize, torrentSize - offset);

            return Read(files, offset, buffer, bufferOffset, count, pieceLength, torrentSize);
        }

        internal bool Read(IList<TorrentFile> files, long offset, byte[] buffer, int bufferOffset, int count, int pieceLength, long torrentSize)
        {
            if (offset < 0 || offset + count > torrentSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int totalRead = 0;

            for (i = 0; i < files.Count; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalRead < count)
            {
                int fileToRead = (int)Math.Min(files[i].Length - offset, count - totalRead);
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

            return true;
        }

        public abstract int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count);

        public abstract void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count);

        internal void Write(IList<TorrentFile> files, long offset, byte[] buffer, int bufferOffset, int count, int pieceLength, long torrentSize)
        {
            if (offset < 0 || offset + count > torrentSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int totalWritten = 0;

            for (i = 0; i < files.Count; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalWritten < count)
            {
                int fileToWrite = (int)Math.Min(files[i].Length - offset, count - totalWritten);
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
        }
    }
}