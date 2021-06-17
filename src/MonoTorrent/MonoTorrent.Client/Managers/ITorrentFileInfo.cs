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


using System.Diagnostics;
using System.Threading;

namespace MonoTorrent.Client
{

    public interface ITorrentFileInfo : ITorrentFile
    {
        BitField BitField { get; }

        /// <summary>
        /// The full path to the file on disk. Can be modified by calling <see cref="TorrentManager.MoveFileAsync(ITorrentFileInfo, string)" />
        /// or <see cref="TorrentManager.MoveFilesAsync(string, bool)"/>.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// The priority of the file when downloading. Can be modified by calling <see cref="TorrentManager.SetFilePriorityAsync(ITorrentFileInfo, Priority)"/>
        /// </summary>
        Priority Priority { get; }
    }

    public static class ITorrentFileInfoExtensions
    {
        public static long BytesDownloaded (this ITorrentFileInfo info)
            => (long) (info.BitField.PercentComplete * info.Length / 100.0);
    }
}
