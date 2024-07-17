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
using System.Linq;
using System.Runtime;

namespace MonoTorrent.Client
{
    class TorrentFileInfo : ITorrentManagerFile
    {
        public static string IncompleteFileSuffix => ".!mt";

        public string DownloadCompleteFullPath { get; private set; }

        public string DownloadIncompleteFullPath { get; private set; }

        public string FullPath { get; private set; }

        ITorrentFile TorrentFile { get; }

        internal BitField BitField { get; }
        ReadOnlyBitField ITorrentManagerFile.BitField => BitField;

        public Priority Priority { get; set; } = Priority.Normal;

        public string Path => TorrentFile.Path;

        public int StartPieceIndex => TorrentFile.StartPieceIndex;

        public long OffsetInTorrent => TorrentFile.OffsetInTorrent;

        public int EndPieceIndex => TorrentFile.EndPieceIndex;

        public long Length => TorrentFile.Length;

        public long Padding => TorrentFile.Padding;

        public int PieceCount => TorrentFile.PieceCount;

        public MerkleRoot PiecesRoot => TorrentFile.PiecesRoot;

        public TorrentFileInfo (ITorrentFile torrentFile, string fullPath)
        {
            TorrentFile = torrentFile;
            FullPath = DownloadCompleteFullPath = DownloadIncompleteFullPath = fullPath;
            BitField = new BitField (torrentFile.EndPieceIndex - torrentFile.StartPieceIndex + 1);
        }

        public (int startPiece, int endPiece) GetSelector ()
            => (StartPieceIndex, EndPieceIndex);


        internal static TorrentFileInfo[] Create (int pieceLength, params long[] sizes)
            => Create (pieceLength, sizes.Select ((size, index) => ("File_" + index, size, 0, "full/path/to/File_" + index)).ToArray ());

        internal static TorrentFileInfo[] Create (int pieceLength, params (string torrentPath, long size, string fullPath)[] infos)
            => Create (pieceLength, infos.Select (t => (t.torrentPath, t.size, 0, t.fullPath)).ToArray ());

        internal static TorrentFileInfo[] Create (int pieceLength, params (string torrentPath, long size, int padding, string fullPath)[] infos)
        {
            // TorrentFile.Create can reorder the files if there are any of length zero.
            var torrentFiles = MonoTorrent.TorrentFile.Create (pieceLength, infos.Select (t => (t.torrentPath, t.size, t.padding)).ToArray ());
            return torrentFiles.Select (t => {
                var info = infos.Single (info => info.torrentPath == t.Path);
                return new TorrentFileInfo (t, info.fullPath);
            }).ToArray ();
        }

        internal static string PathEscape (string path)
        {
            foreach (var illegal in System.IO.Path.GetInvalidPathChars ())
                path = path.Replace ($"{illegal}", Convert.ToString (illegal, 16));
            return path;
        }

        internal static string PathAndFileNameEscape (string path)
        {
            var probableFilenameIndex = path.LastIndexOf (System.IO.Path.DirectorySeparatorChar);
            var dir = probableFilenameIndex == -1 ? "" : path.Substring (0, probableFilenameIndex);
            var filename = probableFilenameIndex == -1 ? path : path.Substring (probableFilenameIndex + 1);

            dir = PathEscape (dir);

            foreach (var illegal in System.IO.Path.GetInvalidFileNameChars ())
                filename = filename.Replace ($"{illegal}", $"_{Convert.ToString (illegal, 16)}_");
            return System.IO.Path.Combine (dir, filename);
        }

        internal static (string path, string completePath, string incompletePath) GetNewPaths (string newPath, bool usePartialFiles, bool isComplete)
        {
            var downloadCompleteFullPath = newPath;
            var downloadIncompleteFullPath = downloadCompleteFullPath + (usePartialFiles ? TorrentFileInfo.IncompleteFileSuffix : "");
            newPath = isComplete ? downloadCompleteFullPath : downloadIncompleteFullPath;

            return (newPath, downloadCompleteFullPath, downloadIncompleteFullPath);
        }

        internal void UpdatePaths ((string newPath, string downloadCompletePath, string downloadIncompletePath) paths)
        {
            FullPath = paths.newPath;
            DownloadCompleteFullPath = paths.downloadCompletePath;
            DownloadIncompleteFullPath = paths.downloadIncompletePath;
        }
    }
}
