//
// RemoveMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
    public enum RemoveMode
    {
        /// <summary>
        /// Does not remove any cache data, such as fast resume data and the copy of the .torrent metadata,
        /// from the <see cref="EngineSettings.CacheDirectory"/> when removing the <see cref="TorrentManager"/>
        /// from the <see cref="ClientEngine"/>. Any data downloaded by the <see cref="TorrentManager"/> will not be deleted.
        /// </summary>
        KeepAllData = 0,

        /// <summary>
        /// Removes all cache data, such as fast resume data and the copy of the .torrent metadata,
        /// from the <see cref="EngineSettings.CacheDirectory"/> when removing the <see cref="TorrentManager"/>
        /// from the <see cref="ClientEngine"/>. Any data downloaded by the <see cref="TorrentManager"/> will not be deleted.
        /// </summary>
        CacheDataOnly = 1 << 0,

        /// <summary>
        /// Any data downloaded by the <see cref="TorrentManager"/> will be deleted. Does not remove any cache data,
        /// such as fast resume data and the copy of the .torrent metadata, from the <see cref="EngineSettings.CacheDirectory"/>
        /// when removing the <see cref="TorrentManager"/> from the <see cref="ClientEngine"/>.
        /// </summary>
        DownloadedDataOnly = 1 << 1,

        /// <summary>
        /// Removes all cache data from the <see cref="EngineSettings.CacheDirectory"/> when removing the <see cref="TorrentManager"/>
        /// from the <see cref="ClientEngine"/>. Any data downloaded by the <see cref="TorrentManager"/> will be deleted.
        /// </summary>
        CacheDataAndDownloadedData = CacheDataOnly | DownloadedDataOnly,
    }
}