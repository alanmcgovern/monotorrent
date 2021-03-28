using System;
using System.Collections.Generic;
using System.Text;

using MonoTorrent.Client;

namespace MonoTorrent
{
    static class TorrentFileExtensions
    {
        static readonly Func<ITorrentFileInfo, (long offset, int pieceLength), int> OffsetComparator = (file, offsetAndPieceLength) => {
            (long torrentOffset, int pieceLength) = offsetAndPieceLength;
            var fileStart = file.OffsetInTorrent;
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
        internal static int FindFileByOffset (this IList<ITorrentFileInfo> files, long offset, int pieceLength)
        {
            var firstMatch = files.BinarySearch (OffsetComparator, (offset, pieceLength));
            while (firstMatch > 0) {
                var previous = files[firstMatch - 1];
                if (previous.OffsetInTorrent >= offset) {
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
        internal static int FindFileByPieceIndex (this IList<ITorrentFileInfo> files, int pieceIndex)
        {
            var firstMatch = files.BinarySearch (PieceIndexComparator, (pieceIndex));
            while (firstMatch > 0) {
                var previous = files[firstMatch - 1];
                if (previous.EndPieceIndex >= pieceIndex) {
                    firstMatch--;
                } else {
                    break;
                }
            }
            return firstMatch;
        }
    }
}
