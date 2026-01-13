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
using System.Linq;
using System.Text;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace MonoTorrent.Client
{
    class TorrentFileInfo : ITorrentManagerFile
    {
        static readonly Dictionary<char, string> InvalidCharsReplaces;
#if NET8_0_OR_GREATER
        static readonly SearchValues<char> InvalidChars;
#else
        static readonly char[] InvalidChars;
#endif
        static TorrentFileInfo ()
        {
            var chars = System.IO.Path.GetInvalidFileNameChars ()
                .Where (a => a != System.IO.Path.DirectorySeparatorChar && a != System.IO.Path.AltDirectorySeparatorChar).ToArray ();
#if NET8_0_OR_GREATER
            InvalidChars = SearchValues.Create (chars);
#else
            InvalidChars = chars;
#endif
            InvalidCharsReplaces = chars.ToDictionary (a => a, a => $"_{Convert.ToString (a, 16)}_");
        }

        public static string IncompleteFileSuffix => ".!mt";

        public SpanStringList DownloadCompleteFullPath { get; private set; }

        public SpanStringList DownloadIncompleteFullPath { get; private set; }

        public SpanStringList FullPath { get; private set; }

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

        public TorrentFileInfo (ITorrentFile torrentFile, SpanStringList fullPath)
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
            int index = path.AsSpan ().IndexOfAny (InvalidChars);
            if (index == -1)
                return path;
            ReadOnlySpan<char> remaining = path.AsSpan ();
            var sb = new StringBuilder ();
            int start = 0;
            int length = path.Length;
            while (index >= 0) {
                sb.Append (path, start, index);
                sb.Append (InvalidCharsReplaces[remaining[index]]);
                remaining = remaining.Slice (index + 1);
                start = start + index + 1;
                length = length - index - 1;
                index = remaining.IndexOfAny (InvalidChars);
            }

            sb.Append (path, start, length);

            return sb.ToString ();
        }

        internal static SpanStringList PathAndFileNameEscape (string path)
        {
            int index = path.AsSpan ().IndexOfAny (InvalidChars);
            if (index == -1)
                return new SpanStringList (path);
            ReadOnlySpan<char> remaining = path.AsSpan ();
            SpanStringList? sb = null;
            int start = 0;
            var length = path.Length;
            while (index >= 0) {
                if (sb is null) {
                    sb = new SpanStringList (path, start, index);
                } else {
                    sb = sb.Append (path, start, index);
                }

                sb = sb.Append (InvalidCharsReplaces[remaining[index]]);
                remaining = remaining.Slice (index + 1);
                start = start + index + 1;
                length = length - index - 1;
                index = remaining.IndexOfAny (InvalidChars);
            }

            sb = sb.Append (path, start, length);

            return sb;
        }

        internal static (SpanStringList path, SpanStringList completePath, SpanStringList incompletePath) GetNewPaths (string newPath, bool usePartialFiles, bool isComplete)
        {
            var path = new SpanStringList (newPath);
            var downloadCompleteFullPath = path;
            SpanStringList downloadIncompleteFullPath;
            if (usePartialFiles) {
                downloadIncompleteFullPath = downloadCompleteFullPath;
                downloadIncompleteFullPath = downloadIncompleteFullPath.Append (TorrentFileInfo.IncompleteFileSuffix);
            } else {
                downloadIncompleteFullPath = downloadCompleteFullPath;
            }

            path = isComplete ? downloadCompleteFullPath : downloadIncompleteFullPath;

            return (path, downloadCompleteFullPath, downloadIncompleteFullPath);
        }

        internal static (SpanStringList path, SpanStringList completePath, SpanStringList incompletePath) GetNewPaths (string containingDirectory, string newPath, bool usePartialFiles, bool isComplete)
        {
            var path = new SpanStringList (containingDirectory);
            path = path.Append (System.IO.Path.DirectorySeparatorChar.ToString ());
            path = path.Append(newPath);
            var downloadCompleteFullPath = path;
            SpanStringList downloadIncompleteFullPath;
            if (usePartialFiles) {
                downloadIncompleteFullPath = downloadCompleteFullPath;
                downloadIncompleteFullPath = downloadIncompleteFullPath.Append(TorrentFileInfo.IncompleteFileSuffix);
            } else {
                downloadIncompleteFullPath = downloadCompleteFullPath;
            }

            path = isComplete ? downloadCompleteFullPath : downloadIncompleteFullPath;

            return (path, downloadCompleteFullPath, downloadIncompleteFullPath);
        }

        internal void UpdatePaths ((SpanStringList newPath, SpanStringList downloadCompletePath, SpanStringList downloadIncompletePath) paths)
        {
            FullPath = paths.newPath;
            DownloadCompleteFullPath = paths.downloadCompletePath;
            DownloadIncompleteFullPath = paths.downloadIncompletePath;
        }
    }
}
