//
// ITorrentInfo.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2022 Alan McGovern
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


using System.Collections.Generic;

namespace MonoTorrent
{
    public interface ITorrentInfo
    {
        /// <summary>
        /// The files contained within the Torrent
        /// </summary>
        IList<ITorrentFile> Files { get; }

        /// <summary>
        /// The SHA1 hash for this torrent. Used by torrents which comply with the v1 specification, or hybrid v1/v2 torrents.
        /// </summary>
        InfoHash InfoHash { get; }

        /// <summary>
        /// The SHA256 hash for this torrent. Used by torrents which comply with the v2 specification, or hybrid v1/v2 torrents.
        /// </summary>
        InfoHash InfoHashV2 { get; }

        /// <summary>
        /// The name of the Torrent.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The size, in bytes, of each piece. The final piece may be smaller.
        /// </summary>
        int PieceLength { get; }

        /// <summary>
        /// The size, in bytes, of the torrent.
        /// </summary>
        long Size { get; }
    }

    public static class ITorrentInfoExtensions
    {
        public static int BlocksPerPiece (this ITorrentInfo self, int pieceIndex)
        {
            if (pieceIndex < self.PieceCount () - 1)
                return (Constants.BlockSize - 1 + self.PieceLength) / Constants.BlockSize;

            var remainder = self.Size - self.PieceIndexToByteOffset (pieceIndex);
            return (int) ((remainder + Constants.BlockSize - 1) / Constants.BlockSize);
        }

        public static int BytesPerPiece (this ITorrentInfo self, int pieceIndex)
        {
            if (pieceIndex < self.PieceCount () - 1)
                return self.PieceLength;
            return (int) (self.Size - self.PieceIndexToByteOffset (pieceIndex));
        }

        public static int ByteOffsetToPieceIndex (this ITorrentInfo self, long offset)
            => (int) (offset / self.PieceLength);

        /// <summary>
        /// The number of pieces in the torrent
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int PieceCount (this ITorrentInfo self)
            => (int) ((self.Size + self.PieceLength - 1) / self.PieceLength);

        public static long PieceIndexToByteOffset (this ITorrentInfo self, int pieceIndex)
            => (long) self.PieceLength * pieceIndex;
    }
}
