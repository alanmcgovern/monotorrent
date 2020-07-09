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
using System.Text;

namespace MonoTorrent
{
    public sealed class TorrentFile : IEquatable<TorrentFile>, ITorrentFile
    {
        /// <summary>
        /// The ED2K hash of the file
        /// </summary>
        public byte[] ED2K { get; }

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
        public byte[] MD5 { get; }

        /// <summary>
        /// In the case of a single torrent file, this is the name of the file.
        /// In the case of a multi-file torrent this is the relative path of the file
        /// (including the filename) from the base directory
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The SHA1 hash of the file
        /// </summary>
        public byte[] SHA1 { get; }

        /// <summary>
        /// The index of the first piece of this file
        /// </summary>
        public int StartPieceIndex { get; }

        /// <summary>
        /// Piece byte offset of the file
        /// </summary>
        public int StartPieceOffset { get; }

        internal TorrentFile (string path, long length)
            : this (path, length, 0, 0)
        {

        }

        internal TorrentFile (string path, long length, int startIndex, int endIndex)
            : this (path, length, startIndex, endIndex, 0, null, null, null)
        {

        }

        internal TorrentFile (string path, long length, int startIndex, int endIndex, int startOffset, byte[] md5, byte[] ed2k, byte[] sha1)
        {
            ED2K = ed2k;
            EndPieceIndex = endIndex;
            Length = length;
            MD5 = md5;
            Path = path;
            SHA1 = sha1;
            StartPieceIndex = startIndex;
            StartPieceOffset = startOffset;
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
    }
}