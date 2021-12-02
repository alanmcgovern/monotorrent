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

using ReusableTasks;

namespace MonoTorrent.PieceWriter
{
    static class IPieceWriterExtensions
    {
        public static async ReusableTask<int> ReadFromFilesAsync (this IPieceWriter writer, ITorrentData manager, BlockInfo request, Memory<byte> buffer)
        {
            var count = request.RequestLength;
            var offset = request.ToByteOffset (manager.PieceLength);

            if (count < 1)
                throw new ArgumentOutOfRangeException (nameof (count), $"Count must be greater than zero, but was {count}.");

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (offset));

            int totalRead = 0;
            var files = manager.Files;
            int i = manager.Files.FindFileByOffset (offset);
            offset -= files[i].OffsetInTorrent;

            while (totalRead < count) {
                int fileToRead = (int) Math.Min (files[i].Length - offset, count - totalRead);
                fileToRead = Math.Min (fileToRead, Constants.BlockSize);

                if (fileToRead != await writer.ReadAsync (files[i], offset, buffer.Slice (totalRead, fileToRead)))
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

        public static async ReusableTask WriteToFilesAsync (this IPieceWriter writer, ITorrentData manager, BlockInfo request, Memory<byte> buffer)
        {
            var count = request.RequestLength;
            var torrentOffset = request.ToByteOffset (manager.PieceLength);
            if (torrentOffset < 0 || torrentOffset + count > manager.Size)
                throw new ArgumentOutOfRangeException (nameof (request));

            int totalWritten = 0;
            var files = manager.Files;
            int i = files.FindFileByOffset (torrentOffset);
            var offset = torrentOffset - files[i].OffsetInTorrent;

            while (totalWritten < count) {
                int fileToWrite = (int) Math.Min (files[i].Length - offset, count - totalWritten);
                fileToWrite = Math.Min (fileToWrite, Constants.BlockSize);

                await writer.WriteAsync (files[i], offset, buffer.Slice (totalWritten, fileToWrite));
                offset += fileToWrite;
                totalWritten += fileToWrite;
                if (offset >= files[i].Length) {
                    offset = 0;
                    i++;
                }
            }
        }
    }
}
