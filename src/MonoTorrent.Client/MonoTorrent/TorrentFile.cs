//
// TorrentFile.cs
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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoTorrent
{
    // l = symlink, x = executable, h = hidden, p = padding file. Characters appear in no particular order and unknown characters should be ignored.
    [Flags]
    public enum TorrentFileAttributes
    {
        None = 0,
        Symlink = 1,
        Executable = 2,
        Hidden = 4,
        Padding = 8,
    };

    internal class TorrentFileTuple
    {
        public string? path = default;
        public long length = 0;
        public long padding = 0;
        public ReadOnlyMemory<byte> md5sum = default;
        public ReadOnlyMemory<byte> ed2k = default;
        public ReadOnlyMemory<byte> sha1 = default;
        public TorrentFileAttributes attributes = TorrentFileAttributes.None;
        public MerkleRoot MerkleRoot;
    }

    public sealed class TorrentFile : IEquatable<TorrentFile>, ITorrentFile
    {
        /// <summary>
        /// The index of the last piece of this file
        /// </summary>
        public int EndPieceIndex { get; }

        /// <summary>
        /// The length of the file in bytes
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// bep-0047 padding length in bytes
        /// </summary>
        public long Padding { get; }

        /// <summary>
        /// In the case of a single torrent file, this is the name of the file.
        /// In the case of a multi-file torrent this is the relative path of the file
        /// (including the filename) from the base directory
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Returns the number of pieces for this file. This is the same as `<see cref="EndPieceIndex"/> - <see cref="StartPieceIndex"/> + 1`.
        /// </summary>
        public int PieceCount => EndPieceIndex - StartPieceIndex + 1;

        /// <summary>
        /// The index of the first piece of this file
        /// </summary>
        public int StartPieceIndex { get; }


        /// <summary>
        /// The offset to the start point of the files data within the torrent, in bytes.
        /// </summary>
        public long OffsetInTorrent { get; }

        public MerkleRoot PiecesRoot { get; }

        public TorrentFileAttributes Attributes { get; }

        internal TorrentFile (string path, long length, int startIndex, int endIndex, long offsetInTorrent, TorrentFileAttributes attributes, long padding)
            : this (path, length, startIndex, endIndex, offsetInTorrent, MerkleRoot.Empty, attributes, padding)
        {
        }

        internal TorrentFile (string path, long length, int startIndex, int endIndex, long offsetInTorrent, MerkleRoot piecesRoot, TorrentFileAttributes attributes, long padding)
        {
            Path = path;
            Length = length;
            Padding = padding;

            StartPieceIndex = startIndex;
            EndPieceIndex = endIndex;
            OffsetInTorrent = offsetInTorrent;

            PiecesRoot = piecesRoot;
            Attributes = attributes;
        }

        public override bool Equals (object? obj)
            => Equals (obj as TorrentFile);

        public bool Equals (TorrentFile? other)
            => Path == other?.Path && Length == other.Length;

        public override int GetHashCode ()
            => Path.GetHashCode ();

        public override string ToString ()
        {
            var sb = new StringBuilder (32);
            sb.Append ("File: ");
            sb.Append (Path);
            sb.Append (" StartIndex: ");
            sb.Append (StartPieceIndex);
            sb.Append (" EndIndex: ");
            sb.Append (EndPieceIndex);
            return sb.ToString ();
        }

        internal static ITorrentFile[] Create (int pieceLength, params long[] lengths)
            => Create (pieceLength, lengths.Select ((length, index) => ("File_" + index, length)).ToArray ());

        internal static ITorrentFile[] Create (int pieceLength, params (string torrentPath, long length)[] files)
            => Create (pieceLength, files.Select (t => new TorrentFileTuple { path = t.torrentPath, length = t.length, padding = 0 }).ToArray ());

        internal static ITorrentFile[] Create (int pieceLength, params (string torrentPath, long length, int padding)[] files)
            => Create (pieceLength, files.Select (t => new TorrentFileTuple { path = t.torrentPath, length = t.length, padding = t.padding }).ToArray ());

        internal static ITorrentFile[] Create (int pieceLength, TorrentFileTuple[] files)
        {
            long totalSize = 0;

            // register padding file byte counts into padding field of the real predecessor file
            for (int t = 0, real = -1; t < files.Length; t++) {
                if ((files[t].attributes & TorrentFileAttributes.Padding) != 0) {
                    if (real < 0) {
                        // this will only happen if the first file is a padding file, bep-0047 doesn't seem to forbid that
                        // Do not try to handle this, it would be very messy.
                        throw new ArgumentException ("Can not handle torrent that starts with padding");
                    } else {
                        if (files[real].length == 0)
                            throw new InvalidOperationException ("Attempted to add padding bytes to a file which should not have padding.");
                        // add the count to it will also work in case of consecutive padding files, also slightly edge-case-y
                        files[real].padding += files[t].length;
                    }
                } else {
                    real = t;
                }
            }

            // now we can forget the padding tuples
            files = files.Where (f => (f.attributes & TorrentFileAttributes.Padding) == 0).ToArray ();

            var results = new List<ITorrentFile> (files.Length);
            for (int i = 0; i < files.Length; i++) {
                // If a file is of length zero, it's should be treated as being part of the first piece.
                if (files[i].length == 0) {
                    results.Add (new TorrentFile (files[i].path!, 0, 0, 0, 0, files[i].attributes, 0));
                    continue;
                }

                var length = files[i].length;
                var padding = files[i].padding;

                var pieceStart = (int) (totalSize / pieceLength);
                var pieceEnd = length > 0 ? (int) ((totalSize + length - 1) / pieceLength) : pieceStart;
                var pieceEndWithPadding = (length + padding) > 0 ? (int) ((totalSize + (length + padding) - 1) / pieceLength) : pieceStart;
                var startOffsetInTorrent = totalSize;
                // catch pathological case of too much padding
                if (pieceEnd != pieceEndWithPadding) {
                    throw new ArgumentException ("A file in the torrent has more padding than needed.");
                }

                results.Add (new TorrentFile (files[i].path!, length, pieceStart, pieceEnd, startOffsetInTorrent, files[i].attributes, padding));
                totalSize += (length + padding);
            }
            Sort (results);
            return results.ToArray ();
        }

        internal static void Sort (List<ITorrentFile> files)
        {
            // Ensure files are always sorted so that 'OffsetInTorrent' is always increasing. If files have the same starting 'OffsetInTorrent'
            // then they are of length 0, and so should be sorted based on their length, so empty files are first. If there are multiple
            // empty files then sort them by path so we always have deterministic sorting.
            files.Sort ((left, right) => {
                var comparison = left.OffsetInTorrent.CompareTo (right.OffsetInTorrent);
                if (comparison == 0)
                    comparison = left.Length.CompareTo (right.Length);
                if (comparison == 0)
                    comparison = left.Path.CompareTo (right.Path);
                return comparison;
            });
        }
    }
}
