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

namespace MonoTorrent.PieceWriter
{
    public interface IBlockCache : IDisposable
    {
        /// <summary>
        /// This event is raised every time a block is successfully read from the cache
        /// </summary>
        event EventHandler<BlockInfo> ReadFromCache;
        /// <summary>
        /// This event is raised every time a block cannot be read from the cache, and is read from
        /// the underlying <see cref="IPieceWriter"/> instead.
        /// </summary>
        event EventHandler<BlockInfo> ReadThroughCache;

        /// <summary>
        /// This event is raised when a block is written to the cache.
        /// </summary>
        event EventHandler<BlockInfo> WrittenToCache;
        /// <summary>
        /// This event is raised when a new block is written directly by the underlying <see cref="IPieceWriter"/>,
        /// or when a block is removed from the cache and is written by the underlying <see cref="IPieceWriter"/>.
        /// </summary>
        event EventHandler<BlockInfo> WrittenThroughCache;

        /// <summary>
        /// The number of bytes read from the cache.
        /// </summary>
        long CacheHits { get; }

        /// <summary>
        /// The number of bytes currently used by the cache.
        /// </summary>
        long CacheUsed { get; }

        /// <summary>
        /// The capacity of the cache, in bytes.
        /// </summary>
        long Capacity { get; }

        CachePolicy Policy { get; }

        /// <summary>
        /// Pieces will be written to this <see cref="IPieceWriter"/> when they are evicted from the cache.
        /// </summary>
        IPieceWriter Writer { get; }

        /// <summary>
        /// Reads data from the cache and flushes it to disk, or reads the data from disk if it is not available in the cache.
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask<bool> ReadAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer);

        /// <summary>
        /// If the block of data is available in the cache, the data is read into the buffer and the method returns true.
        /// If the block is unavailable, the buffer will not be modified and the method will return false.
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        ReusableTask<bool> ReadFromCacheAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer);

        /// <summary>
        /// Set the max capacity, in bytes.
        /// </summary>
        /// <param name="capacity"></param>
        /// <returns></returns>
        ReusableTask SetCapacityAsync (long capacity);

        /// <summary>
        /// Sets the cache policy.
        /// </summary>
        /// <param name="policy"></param>
        /// <returns></returns>
        ReusableTask SetPolicyAsync (CachePolicy policy);

        ReusableTask SetWriterAsync (IPieceWriter writer);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrent"></param>
        /// <param name="block"></param>
        /// <param name="buffer"></param>
        /// <param name="preferSkipCache"></param>
        /// <returns></returns>
        ReusableTask WriteAsync (ITorrentManagerInfo torrent, BlockInfo block, Memory<byte> buffer, bool preferSkipCache);
    }
}
