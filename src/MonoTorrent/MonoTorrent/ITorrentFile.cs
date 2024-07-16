//
// ITorrentFile.cs
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

namespace MonoTorrent
{
    public interface ITorrentFile
    {
        /// <summary>
        /// The relative path to the file within the torrent.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// The first piece which contains data for this file
        /// </summary>
        int StartPieceIndex { get; }

        /// <summary>
        /// The last piece which contains data for this file.
        /// </summary>
        int EndPieceIndex { get; }

        /// <summary>
        /// Returns the number of pieces for this file. This is the same as `<see cref="EndPieceIndex"/> - <see cref="StartPieceIndex"/> + 1`.
        /// </summary>
        int PieceCount { get; }

        /// <summary>
        /// The size of this file in bytes.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// bep-0047 padding.
        /// </summary>
        long Padding { get; }

        /// <summary>
        /// The offset, relative to the first byte in the torrent, where this file begins.
        /// </summary>
        long OffsetInTorrent { get; }

        /// <summary>
        /// The root of the merkle tree constructed for this file. Generated using a SHA256 hash by BEP52 compliant torrents.
        /// </summary>
        MerkleRoot PiecesRoot { get; }
    }
}
