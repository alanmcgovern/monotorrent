//
// PieceHashedEventArgs.cs
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


namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides the data needed to handle a PieceHashed event
    /// </summary>
    public sealed class PieceHashedEventArgs : TorrentEventArgs
    {
        /// <summary>
        /// The index of the piece which was hashed
        /// </summary>
        public int PieceIndex { get; }

        /// <summary>
        /// The value of whether the piece passed or failed the hash check
        /// </summary>
        public bool HashPassed { get; }

        /// <summary>
        /// If the TorrentManager is in the hashing state then this returns a value between 0 and 1 indicating
        /// how complete the hashing progress is. If the manager is in the Downloading state then this will
        /// return '1' as the torrent will have been fully hashed already. If some files in the torrent were
        /// marked as 'DoNotDownload' during the initial hash, and those files are later marked as downloadable,
        /// then this will still return '1'.
        /// </summary>
        public double Progress { get; }

        /// <summary>
        /// Creates a new PieceHashedEventArgs
        /// </summary>
        /// <param name="manager">The <see cref="TorrentManager"/> whose piece was hashed</param>
        /// <param name="pieceIndex">The index of the piece that was hashed</param>
        /// <param name="hashPassed">True if the piece passed the hashcheck, false otherwise</param>
        internal PieceHashedEventArgs (TorrentManager manager, int pieceIndex, bool hashPassed)
            : this (manager, pieceIndex, hashPassed, 1, 1)
        {

        }

        internal PieceHashedEventArgs (TorrentManager manager, int pieceIndex, bool hashPassed, int piecesHashed, int totalToHash)
            : base (manager)
        {
            PieceIndex = pieceIndex;
            HashPassed = hashPassed;
            Progress = (double) piecesHashed / totalToHash;
        }
    }
}
