//
// TorrentFileInfo.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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

namespace MonoTorrent.Client
{
    class TorrentFileInfo : ITorrentFileInfo
    {
        public string FullPath { get; set; }

        ITorrentFile TorrentFile { get; }

        public BitField BitField { get; }

        public Priority Priority { get; set; } = Priority.Normal;

        public string Path => TorrentFile.Path;

        public int StartPieceIndex => TorrentFile.StartPieceIndex;

        public int StartPieceOffset => TorrentFile.StartPieceOffset;

        public int EndPieceIndex => TorrentFile.EndPieceIndex;

        public long Length => TorrentFile.Length;

        public TorrentFileInfo (ITorrentFile torrentFile)
            : this (torrentFile, torrentFile.Path)
        {
        }

        public TorrentFileInfo (ITorrentFile torrentFile, string fullPath)
        {
            TorrentFile = torrentFile;
            FullPath = fullPath;
            BitField = new BitField (torrentFile.EndPieceIndex - torrentFile.StartPieceIndex + 1);
        }

        public (int startPiece, int endPiece) GetSelector ()
            => (StartPieceIndex, EndPieceIndex);


        internal static TorrentFileInfo[] Create (int pieceLength, params int[] sizes)
        {
            var totalSize = 0;

            var files = new List<TorrentFileInfo> ();
            for (int i = 0; i < sizes.Length; i++) {
                var size = sizes[i];
                var pieceStart = totalSize / pieceLength;
                var pieceEnd = (totalSize + size) / pieceLength;
                var startOffset = totalSize - (totalSize / pieceLength) * pieceLength;
                if ((totalSize + size) % pieceLength == 0) {
                    if (size == 0) {
                        startOffset = pieceLength - 1;
                        pieceStart--;
                    }
                    pieceEnd--;
                }
                files.Add (new TorrentFileInfo (new TorrentFile ("File_" + i, size, pieceStart, pieceEnd, startOffset, null, null, null), "full/path/File_" + i));
                totalSize += size;
            }
            return files.ToArray ();
        }
    }
}
