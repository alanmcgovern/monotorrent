using System;
using System.Collections.Generic;

namespace MonoTorrent
{
    static class TorrentFileExtensions
    {
        static readonly Func<ITorrentManagerFile, long, int> OffsetComparator = (file, offset) => {
            var fileStart = file.OffsetInTorrent;
            var fileEnd = fileStart + file.Length + file.Padding;
            if (offset >= fileStart && offset < fileEnd)
                return 0;
            if (offset >= fileEnd)
                return -1;
            else
                return 1;
        };

        static readonly Func<ITorrentManagerFile, int, int> PieceIndexComparator = (file, pieceIndex) => {
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
        /// <returns></returns>
        internal static int FindFileByOffset (this IList<ITorrentManagerFile> files, long offset)
        {
            var firstMatch = files.BinarySearch (OffsetComparator, offset);
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
        internal static int FindFileByPieceIndex (this IList<ITorrentManagerFile> files, int pieceIndex)
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
