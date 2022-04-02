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


using System;
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
        /// The infohashes for this torrent.
        /// </summary>
        InfoHashes InfoHashes { get; }

        /// <summary>
        /// The name of the Torrent.
        /// </summary>
        string? Name { get; }

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
        static bool IsV1Only (this ITorrentInfo self)
            => self.InfoHashes.V1 != null && self.InfoHashes.V2 == null;

        static bool IsV2Only (this ITorrentInfo self)
            => self.InfoHashes.V1 == null && self.InfoHashes.V2 != null;

        static bool IsHybrid (this ITorrentManagerInfo self)
            => self.InfoHashes.IsHybrid;

        static bool IsHybrid (this ITorrentInfo self)
            => self.InfoHashes.IsHybrid;

        public static int BlocksPerPiece (this ITorrentInfo self, int pieceIndex)
            => (BytesPerPiece (self, pieceIndex) + Constants.BlockSize - 1) / Constants.BlockSize;

        public static int BytesPerPiece (this ITorrentInfo self, int pieceIndex)
        {
            if (self.IsV1Only ()) {
                if (pieceIndex < self.PieceCount () - 1)
                    return self.PieceLength;
                return (int) (self.Size - self.PieceIndexToByteOffset (pieceIndex));
            } else if (self.IsV2Only ()) {
                for (int i = 0; i < self.Files.Count; i++) {
                    var file = self.Files[i];
                    if (pieceIndex < file.StartPieceIndex || pieceIndex > file.EndPieceIndex)
                        continue;
                    var remainder = file.Length - (pieceIndex - file.StartPieceIndex) * self.PieceLength;
                    return (int) (remainder > self.PieceLength ? self.PieceLength : remainder);
                }
                throw new ArgumentOutOfRangeException (nameof (pieceIndex));
            } else {
                throw new NotSupportedException ("hybrid torrents aren't supported");
            }
        }

        public static int ByteOffsetToPieceIndex (this ITorrentInfo self, long offset)
        {
            if (self.IsV1Only ()) {
                return (int) (offset / self.PieceLength);
            } else if (self.IsV2Only ()) {
                for (int i = 0; i < self.Files.Count; i++) {
                    var file = self.Files[i];
                    if (offset < file.OffsetInTorrent || offset >= file.OffsetInTorrent + file.Length)
                        continue;
                    return file.StartPieceIndex + (int) ((offset - file.OffsetInTorrent) / self.PieceLength);
                }
                throw new ArgumentOutOfRangeException (nameof (offset));
            } else {
                throw new NotSupportedException ("Hybrid torrents aren't supported");
            }
        }

        /// <summary>
        /// The number of pieces in the torrent
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int PieceCount (this ITorrentInfo self)
        {
            if (self.IsV1Only ())
                return (int) ((self.Size + self.PieceLength - 1) / self.PieceLength);
            else if (self.IsV2Only ())
                return self.Files[self.Files.Count - 1].EndPieceIndex + 1;
            else
                throw new NotSupportedException ();
        }

        public static long PieceIndexToByteOffset (this ITorrentInfo self, int pieceIndex)
        {
            if (self.IsV1Only ()) {
                return (long) (long) self.PieceLength * pieceIndex;
            } else if (self.IsV2Only ()) {
                for (int i = 0; i < self.Files.Count; i++) {
                    var file = self.Files[i];
                    if (pieceIndex < file.StartPieceIndex || pieceIndex > file.EndPieceIndex)
                        continue;
                    return file.OffsetInTorrent + ((pieceIndex - file.StartPieceIndex) * (long) self.PieceLength);
                }
                throw new ArgumentOutOfRangeException (nameof (pieceIndex));
            } else {
                throw new NotSupportedException ("Hybrid torrents aren't supported");
            }
        }
    }
}
