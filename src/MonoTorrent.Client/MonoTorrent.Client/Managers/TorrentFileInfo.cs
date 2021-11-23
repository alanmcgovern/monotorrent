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


using System.Linq;

namespace MonoTorrent.Client
{
    class TorrentFileInfo : ITorrentFileInfo
    {
        public static string IncompleteFileSuffix => ".!mt";

        public string DownloadCompleteFullPath { get; set; }

        public string DownloadIncompleteFullPath { get; set; }

        public string FullPath { get; set; }

        ITorrentFile TorrentFile { get; }

        public BitField BitField { get; }

        public Priority Priority { get; set; } = Priority.Normal;

        public string Path => TorrentFile.Path;

        public int StartPieceIndex => TorrentFile.StartPieceIndex;

        public long OffsetInTorrent => TorrentFile.OffsetInTorrent;

        public int EndPieceIndex => TorrentFile.EndPieceIndex;

        public long Length => TorrentFile.Length;

        public TorrentFileInfo (ITorrentFile torrentFile, string fullPath)
        {
            TorrentFile = torrentFile;
            FullPath = DownloadCompleteFullPath = DownloadIncompleteFullPath = fullPath;
            BitField = new MutableBitField (torrentFile.EndPieceIndex - torrentFile.StartPieceIndex + 1);
        }

        public (int startPiece, int endPiece) GetSelector ()
            => (StartPieceIndex, EndPieceIndex);


        internal static TorrentFileInfo[] Create (int pieceLength, params long[] sizes)
            => Create (pieceLength, sizes.Select ((size, index) => ("File_" + index, size, "full/path/to/File_" + index)).ToArray ());

        internal static TorrentFileInfo[] Create (int pieceLength, params (string torrentPath, long size, string fullPath)[] infos)
        {
            // TorrentFile.Create can reorder the files if there are any of length zero.
            var torrentFiles = MonoTorrent.TorrentFile.Create (pieceLength, infos.Select (t => (t.torrentPath, t.size)).ToArray ());
            return torrentFiles.Select (t => {
                var info = infos.Single (info => info.torrentPath == t.Path);
                return new TorrentFileInfo (t, info.fullPath);
            }).ToArray ();
        }
    }
}
