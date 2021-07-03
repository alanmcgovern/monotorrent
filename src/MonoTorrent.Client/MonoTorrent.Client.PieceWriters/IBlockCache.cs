//
// IPieceCache.cs
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

using ReusableTasks;
using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client.PieceWriters
{
    public interface IBlockCache : IDisposable
    {
        /// <summary>
        /// Reads data from the cache and flushes it to disk, or reads the data from disk if it is not available in the cache.
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask<bool> ReadAsync (ITorrentData torrent, BlockInfo block, byte[] buffer);

        /// <summary>
        /// If the block of data is available in the cache, the data is read into the buffer and the method returns true.
        /// If the block is unavailable, the buffer will not be modified and the method will return false.
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask<bool> ReadFromCacheAsync (ITorrentData torrent, BlockInfo block, byte[] buffer);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <param name="preferSkipCache"></param>
        /// <returns></returns>
        ReusableTask WriteAsync (ITorrentData torrent, BlockInfo block, byte[] buffer, bool preferSkipCache);
    }
}
