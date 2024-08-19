//
// ITorrentFileInfo.cs
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


namespace MonoTorrent
{
    public interface ITorrentManagerFile : ITorrentFile
    {
        /// <summary>
        /// The <see cref="BitField"/> tracking which pieces of this file have been downloaded.
        /// </summary>
        ReadOnlyBitField BitField { get; }

        /// <summary>
        /// If the file is currently being downloaded, this will be the same as <see cref="DownloadIncompleteFullPath"/>. Otherwise it will be <see cref="DownloadCompleteFullPath"/>
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// The file will exist at this path after it has been fully downloaded.
        /// </summary>
        string DownloadCompleteFullPath { get; }

        /// <summary>
        /// The file will exist at this path when it is partially downloaded. This value may be the same as <see cref="DownloadCompleteFullPath"/>.
        /// </summary>
        string DownloadIncompleteFullPath { get; }

        /// <summary>
        /// The priority of the file when downloading.
        /// </summary>
        Priority Priority { get; }
    }

    public static class ITorrentFileInfoExtensions
    {
        public static long BytesDownloaded (this ITorrentManagerFile info)
            => (long) (info.BitField.PercentComplete * info.Length / 100.0);

        public static bool Overlaps (this ITorrentManagerFile self, ITorrentManagerFile file)
            => self.Length > 0 &&
            file.Length > 0 &&
            self.StartPieceIndex <= file.EndPieceIndex &&
            file.StartPieceIndex <= self.EndPieceIndex;
    }
}
