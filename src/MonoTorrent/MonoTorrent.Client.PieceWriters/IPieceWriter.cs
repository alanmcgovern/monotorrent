//
// IPieceWriter.cs
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

using MonoTorrent.Client.PiecePicking;

using ReusableTasks;

namespace MonoTorrent.Client.PieceWriters
{
    static class IPieceWriterExtensions
    {
        static readonly Func<ITorrentFileInfo, (long offset, int pieceLength), int> OffsetComparator = (file, offsetAndPieceLength) => {
            (long torrentOffset, int pieceLength) = offsetAndPieceLength;
            var fileStart = (long) file.StartPieceIndex * pieceLength + file.StartPieceOffset;
            var fileEnd = fileStart + file.Length;
            if (torrentOffset >= fileStart && torrentOffset < fileEnd)
                return 0;
            if (torrentOffset >= fileEnd)
                return -1;
            else
                return 1;
        };

        static readonly Func<ITorrentFileInfo, int, int> PieceIndexComparator = (file, pieceIndex) => {
            if (pieceIndex >= file.StartPieceIndex && pieceIndex <= file.EndPieceIndex)
                return 0;
            if (pieceIndex > file.EndPieceIndex)
                return -1;
            else
                return 1;
        };

        /// <summary>
        /// Used for tests
        /// </summary>
        /// <param name="files"></param>
        /// <param name="offset"></param>
        /// <param name="pieceLength"></param>
        /// <returns></returns>
        public static int FindFileByOffset (IList<ITorrentFileInfo> files, long offset, int pieceLength)
        {
            var firstMatch = files.BinarySearch (OffsetComparator, (offset, pieceLength));
            while (firstMatch > 0) {
                var previous = files[firstMatch - 1];
                if ((long) previous.StartPieceIndex * pieceLength + previous.StartPieceOffset >= offset) {
                    firstMatch--;
                } else {
                    break;
                }
            }
            return firstMatch;
        }

        /// <summary>
        /// Used for tests
        /// </summary>
        /// <param name="files"></param>
        /// <param name="pieceIndex"></param>
        /// <returns></returns>
        public static int FindFileByPieceIndex (IList<ITorrentFileInfo> files, int pieceIndex)
        {
            var firstMatch = files.BinarySearch (PieceIndexComparator, (pieceIndex));
            while (firstMatch > 0) {
                var previous = files[firstMatch - 1];
                if (previous.StartPieceIndex >= pieceIndex) {
                    firstMatch--;
                } else {
                    break;
                }
            }
            return firstMatch;
        }

        public static async ReusableTask<int> ReadFromFilesAsync (this IPieceWriter writer, ITorrentData manager, BlockInfo request, byte[] buffer)
        {
            var count = request.RequestLength;
            var offset = request.ToByteOffset (manager.PieceLength);

            if (count < 1)
                throw new ArgumentOutOfRangeException (nameof (count), $"Count must be greater than zero, but was {count}.");

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (offset));

            int totalRead = 0;
            var files = manager.Files;
            int i = FindFileByOffset (manager.Files, offset, manager.PieceLength);
            offset -= (long) files[i].StartPieceIndex * manager.PieceLength + files[i].StartPieceOffset;

            while (totalRead < count) {
                int fileToRead = (int) Math.Min (files[i].Length - offset, count - totalRead);
                fileToRead = Math.Min (fileToRead, Piece.BlockSize);

                if (fileToRead != await writer.ReadAsync (files[i], offset, buffer, totalRead, fileToRead))
                    return totalRead;

                offset += fileToRead;
                totalRead += fileToRead;
                if (offset >= files[i].Length) {
                    offset = 0;
                    i++;
                }
            }

            return totalRead;
        }

        public static async ReusableTask WriteToFilesAsync (this IPieceWriter writer, ITorrentData manager, BlockInfo request, byte[] buffer)
        {
            var count = request.RequestLength;
            var offset = request.ToByteOffset (manager.PieceLength);
            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (offset));

            int totalWritten = 0;
            var files = manager.Files;
            int i = FindFileByOffset(files, offset, manager.PieceLength);
            offset -= (long) files[i].StartPieceIndex * manager.PieceLength + files[i].StartPieceOffset;

            while (totalWritten < count) {
                int fileToWrite = (int) Math.Min (files[i].Length - offset, count - totalWritten);
                fileToWrite = Math.Min (fileToWrite, Piece.BlockSize);

                await writer.WriteAsync (files[i], offset, buffer, totalWritten, fileToWrite);
                offset += fileToWrite;
                totalWritten += fileToWrite;
                if (offset >= files[i].Length) {
                    offset = 0;
                    i++;
                }
            }
        }
    }

    public interface IPieceWriter : IDisposable
    {
        ReusableTask CloseAsync (ITorrentFileInfo file);
        ReusableTask<bool> ExistsAsync (ITorrentFileInfo file);
        ReusableTask FlushAsync (ITorrentFileInfo file);
        ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite);
        ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count);
        ReusableTask WriteAsync (ITorrentFileInfo file, long offset, byte[] buffer, int bufferOffset, int count);
    }
}
