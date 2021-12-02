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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoTorrent
{
    public sealed class TorrentFile : IEquatable<TorrentFile>, ITorrentFile
    {
        /// <summary>
        /// The ED2K hash of the file
        /// </summary>
        public ReadOnlyMemory<byte> ED2K { get; }

        /// <summary>
        /// The index of the last piece of this file
        /// </summary>
        public int EndPieceIndex { get; }

        /// <summary>
        /// The length of the file in bytes
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// The MD5 hash of the file
        /// </summary>
        public ReadOnlyMemory<byte> MD5 { get; }

        /// <summary>
        /// In the case of a single torrent file, this is the name of the file.
        /// In the case of a multi-file torrent this is the relative path of the file
        /// (including the filename) from the base directory
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The SHA1 hash of the file
        /// </summary>
        public ReadOnlyMemory<byte> SHA1 { get; }

        /// <summary>
        /// The index of the first piece of this file
        /// </summary>
        public int StartPieceIndex { get; }

        /// <summary>
        /// The offset to the start point of the files data within the torrent, in bytes.
        /// </summary>
        public long OffsetInTorrent { get; }

        internal TorrentFile (string path, long length, int startIndex, int endIndex, long offsetInTorrent)
            : this (path, length, startIndex, endIndex, offsetInTorrent, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty)
        {

        }

        internal TorrentFile (string path, long length, int startIndex, int endIndex, long offsetInTorrent, ReadOnlyMemory<byte> md5, ReadOnlyMemory<byte> ed2k, ReadOnlyMemory<byte> sha1)
        {
            Path = path;
            Length = length;

            StartPieceIndex = startIndex;
            EndPieceIndex = endIndex;
            OffsetInTorrent = offsetInTorrent;

            ED2K = ed2k;
            MD5 = md5;
            SHA1 = sha1;
        }

        public override bool Equals (object obj)
            => Equals (obj as TorrentFile);

        public bool Equals (TorrentFile other)
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

        internal static TorrentFile[] Create (int pieceLength, params long[] lengths)
            => Create (pieceLength, lengths.Select ((length, index) => ("File_" + index, length)).ToArray ());

        internal static TorrentFile[] Create (int pieceLength, params (string torrentPath, long length)[] files)
            => Create (pieceLength, files.Select (t => (t.torrentPath, t.length, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty)).ToArray ());

        internal static TorrentFile[] Create (int pieceLength, params (string path, long length, ReadOnlyMemory<byte> md5sum, ReadOnlyMemory<byte> ed2k, ReadOnlyMemory<byte> sha1)[] files)
        {
            long totalSize = 0;
            var results = new List<TorrentFile> (files.Length);
            for (int i = 0; i < files.Length; i++) {
                var length = files[i].length;

                var pieceStart = (int) (totalSize / pieceLength);
                var pieceEnd = (int) ((totalSize + length) / pieceLength);
                var startOffsetInTorrent = totalSize;
                if ((totalSize + length) % pieceLength == 0)
                    pieceEnd--;

                if (length == 0) {
                    pieceStart = i > 0 ? results[i - 1].StartPieceIndex : 0;
                    pieceEnd = i > 0 ? results[i - 1].StartPieceIndex : 0;
                    startOffsetInTorrent = i > 0 ? results[i - 1].OffsetInTorrent : 0;
                }

                results.Add (new TorrentFile (files[i].path, length, pieceStart, pieceEnd, startOffsetInTorrent, files[i].md5sum, files[i].ed2k, files[i].sha1));
                totalSize += length;
            }

            // If a zero length file starts at offset 100, it also ends at offset 100 as it's length is zero.
            // If a non-zero length file starts at offset 100, it will end at a much later offset (for example 1000).
            // In this scenario we want the zero length file to be placed *before* the non-zero length file in this
            // list so we can effectively binary search it later when looking for pieces which begin at a particular offset.
            // The invariant that files later in the list always 'end' at a later point in the file will be maintained.
            results.Sort ((left, right) => {
                var comparison = left.OffsetInTorrent.CompareTo (right.OffsetInTorrent);
                if (comparison == 0)
                    comparison = left.Length.CompareTo (right.Length);
                return comparison;
            });
            return results.ToArray ();
        }
    }
}
