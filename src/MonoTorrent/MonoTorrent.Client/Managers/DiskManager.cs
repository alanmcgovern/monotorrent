//
// DiskManager.cs
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
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.RateLimiters;

namespace MonoTorrent.Client
{
    public class DiskManager : IDisposable
    {
        static readonly ICache<IncrementalHashData> IncrementalHashCache = new Cache<IncrementalHashData> (true);

        readonly Dictionary<int, IncrementalHashData> IncrementalHashes = new Dictionary<int, IncrementalHashData> ();

        class IncrementalHashData : ICacheable
        {
            public SHA1 Hasher;
            public long NextOffsetToHash;

            public IncrementalHashData ()
            {
                Hasher = HashAlgoFactory.Create<SHA1> ();
                Initialise ();
            }

            public void Initialise ()
            {
                Hasher.Initialize ();
                NextOffsetToHash = 0;
            }
        }

        struct BufferedIO
        {
            public ITorrentData manager;
            public long offset;
            public byte [] buffer;
            public int count;
            public TaskCompletionSource<bool> tcs;

            public BufferedIO (ITorrentData manager, long offset, byte [] buffer, int count, TaskCompletionSource<bool> tcs)
            {
                this.manager = manager;
                this.offset = offset;
                this.buffer = buffer;
                this.count = count;
                this.tcs = tcs;
            }
        }

        static readonly MainLoop IOLoop = new MainLoop("Disk IO");

        #region Member Variables

        readonly Queue<BufferedIO> bufferedReads;
        readonly Queue<BufferedIO> bufferedWrites;
        int bufferedWriteBytes;

        readonly SpeedMonitor readMonitor;
        readonly SpeedMonitor writeMonitor;

        internal RateLimiter ReadLimiter { get; }
        internal RateLimiter WriteLimiter { get; }

        #endregion Member Variables


        #region Properties

        bool Disposed { get; set; }

        /// <summary>
        /// The number of bytes which are currently cached in memory, pending writing.
        /// </summary>
        public int BufferedWrites => bufferedWriteBytes;

        /// <summary>
        /// The amount of data, in bytes, being read per second.
        /// </summary>
        public int ReadRate => readMonitor.Rate;

        /// <summary>
        /// The amount of data, in bytes, being written per second.
        /// </summary>
        public int WriteRate => writeMonitor.Rate;

        /// <summary>
        /// The total number of bytes which have been read.
        /// </summary>
        public long TotalRead => readMonitor.Total;

        /// <summary>
        /// The total number of bytes which have been written.
        /// </summary>
        public long TotalWritten => writeMonitor.Total;

        internal IPieceWriter Writer { get; set; }

        #endregion Properties


        #region Constructors

        internal DiskManager(EngineSettings settings, IPieceWriter writer)
        {
            this.bufferedReads = new Queue<BufferedIO>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.ReadLimiter = new RateLimiter();
            this.readMonitor = new SpeedMonitor();
            this.writeMonitor = new SpeedMonitor();
            this.WriteLimiter = new RateLimiter();
            this.Writer = writer;

            IOLoop.QueueTimeout (TimeSpan.FromSeconds (1), () => {
                readMonitor.Tick ();
                writeMonitor.Tick ();

                WriteLimiter.UpdateChunks (settings.MaximumDiskWriteRate, WriteRate);
                ReadLimiter.UpdateChunks (settings.MaximumDiskReadRate, ReadRate);

                ProcessBufferedIO ();

                return !Disposed;
            });
        }

        #endregion Constructors


        #region Methods

        void IDisposable.Dispose ()
            => Dispose ();

        internal void Dispose ()
        {
            if (Disposed)
                return;

            IOLoop.QueueWait (() => {
                ProcessBufferedIO (true);
                Writer.Dispose ();
                Disposed = true;
            });
        }

        #endregion

        internal async Task<bool> CheckFileExistsAsync(TorrentFile file)
        {
            await IOLoop;

            return Writer.Exists(file);
        }

        internal async Task<bool> CheckAnyFilesExistAsync(ITorrentData manager)
        {
            await IOLoop;

            for (int i = 0; i < manager.Files.Length; i++)
                if (Writer.Exists(manager.Files[i]))
                    return true;
            return false;
        }

        internal async Task<byte[]> GetHashAsync(ITorrentData manager, int pieceIndex)
        {
            await IOLoop;

            IncrementalHashData incrementalHash;
            if (IncrementalHashes.TryGetValue (pieceIndex, out incrementalHash)) {
                // We request the blocks for most pieces sequentially, and most (all?) torrent clients
                // will process requests in the order they have been received. This means we can optimise
                // hashing a received piece by hashing each block as it arrives. If blocks arrive out of order then
                // we'll compute the final hash by reading the data from disk.
                if (incrementalHash.NextOffsetToHash == (long)manager.PieceLength * (pieceIndex + 1)
                 || incrementalHash.NextOffsetToHash == manager.Size) {
                    incrementalHash.Hasher.TransformFinalBlock(Array.Empty<byte> (), 0, 0);
                    var result = incrementalHash.Hasher.Hash;
                    IncrementalHashCache.Enqueue (incrementalHash);
                    IncrementalHashes.Remove (pieceIndex);
                    return result;
                }
            } else {
                // If we have no partial hash data for this piece we could be doing a full
                // hash check, so let's create a IncrementalHashData for our piece!
                incrementalHash = IncrementalHashCache.Dequeue ();
                incrementalHash.NextOffsetToHash = (long)manager.PieceLength * pieceIndex;
            }

            // We want to be sure we've actually written everything so when we go to hash the
            // piece it will be returned to us in our Read call. If the write were still pending
            // we could accidentally end up reporting the piece was corrupt.
            await WaitForBufferedWrites();

            // Note that 'startOffset' may not be the very start of the piece if we have a partial hash.
            long startOffset = incrementalHash.NextOffsetToHash;
            long endOffset = Math.Min((long)manager.PieceLength * (pieceIndex + 1), manager.Size);

            byte[] hashBuffer = ClientEngine.BufferManager.GetBuffer(Piece.BlockSize);
            try {
                var hasher = incrementalHash.Hasher;

                while (startOffset != endOffset)
                {
                    int count = (int)Math.Min(Piece.BlockSize, endOffset - startOffset);
                    if (!await ReadAsync(manager, startOffset, hashBuffer, count).ConfigureAwait(false))
                        return null;
                    startOffset += count;
                    hasher.TransformBlock(hashBuffer, 0, count, hashBuffer, 0);
                }

                hasher.TransformFinalBlock(hashBuffer, 0, 0);
                var result = hasher.Hash;
                return result;
            } finally {
                IncrementalHashCache.Enqueue (incrementalHash);
                IncrementalHashes.Remove (pieceIndex);
                ClientEngine.BufferManager.FreeBuffer(hashBuffer);
            }
        }

        async Task WaitForBufferedWrites ()
        {
            if (bufferedWrites.Count > 0)
            {
                TaskCompletionSource<bool> flushed = new TaskCompletionSource<bool>();
                bufferedWrites.Enqueue(new BufferedIO(null, -1, null, -1, flushed));
                await flushed.Task;
            }
        }

        internal async Task CloseFilesAsync(ITorrentData manager)
        {
            await IOLoop;

            // Process all pending reads/writes then close any open streams
            ProcessBufferedIO(true);
            foreach (var file in manager.Files)
                Writer.Close (file);
        }

        internal async Task MoveFileAsync (TorrentFile file, string newPath)
        {
            await IOLoop;

            newPath = Path.GetFullPath (newPath);
            Writer.Move (file, newPath, false);
            file.FullPath = newPath;
        }

        internal async Task MoveFilesAsync(ITorrentData manager, string newRoot, bool overwrite)
        {
            await IOLoop;

            foreach (TorrentFile file in manager.Files) {
                string newPath = Path.Combine (newRoot, file.Path);
                Writer.Move(file, newPath, overwrite);
                file.FullPath = newPath;
            }
        }

        internal async Task<bool> ReadAsync (ITorrentData manager, long offset, byte [] buffer, int count)
        {
            await IOLoop;

            if (ReadLimiter.TryProcess(count))
            {
                return Read(manager, offset, buffer, count);
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                bufferedReads.Enqueue(new BufferedIO(manager, offset, buffer, count, tcs));
                return await tcs.Task;
            }
        }

        internal async Task WriteAsync (ITorrentData manager, long offset, byte[] buffer, int count)
        {
            Interlocked.Add(ref bufferedWriteBytes, count);
            await IOLoop;

            int pieceIndex = (int)(offset / manager.PieceLength);
            long pieceStart = (long) pieceIndex * manager.PieceLength;
            long pieceEnd = pieceStart + manager.PieceLength;

            IncrementalHashData incrementalHash;
            if (!IncrementalHashes.TryGetValue (pieceIndex , out incrementalHash) && offset == pieceStart) {
                incrementalHash = IncrementalHashes[pieceIndex] = IncrementalHashCache.Dequeue ();
                incrementalHash.NextOffsetToHash = (long) manager.PieceLength * pieceIndex;
            }

            if (incrementalHash != null) {
                if ((incrementalHash.NextOffsetToHash + count) > pieceEnd) {
                    IncrementalHashes.Remove (pieceIndex);
                } else if (incrementalHash.NextOffsetToHash == offset) {
                    incrementalHash.Hasher.TransformBlock (buffer, 0, count, buffer, 0);
                    incrementalHash.NextOffsetToHash += count;
                }
            }

            try
            {
                if (WriteLimiter.TryProcess(count))
                {
                    Write(manager, offset, buffer, count);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    bufferedWrites.Enqueue(new BufferedIO(manager, offset, buffer, count, tcs));
                    await tcs.Task;
                }
            } finally {
                Interlocked.Add(ref bufferedWriteBytes, -count);
            }
        }

        void ProcessBufferedIO (bool force = false)
        {
            BufferedIO io;

            while (bufferedWrites.Count > 0) {
                io = bufferedWrites.Peek();
                // This means we wanted to wait until all the writes had been flushed
                // before we attempt to generate the hash of a given piece.
                if (io.manager == null && io.buffer == null)  {
                    io = bufferedWrites.Dequeue();
                    io.tcs.SetResult(true);
                    continue;
                }

                if (!force && !WriteLimiter.TryProcess (io.count))
                    break;

                io = bufferedWrites.Dequeue ();

                try {
                    Write (io.manager, io.offset, io.buffer, io.count);
                    io.tcs.SetResult (true);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }

            while (bufferedReads.Count > 0) {
                if (!force && !ReadLimiter.TryProcess (bufferedReads.Peek ().count))
                    break;

                io = bufferedReads.Dequeue ();

                try {
                    var result = Read (io.manager, io.offset, io.buffer, io.count);
                    io.tcs.SetResult (result);
                } catch (Exception ex) {
                    io.tcs.SetException (ex);
                }
            }
        }

        bool Read (ITorrentData manager, long offset, byte [] buffer, int count)
        {
            readMonitor.AddDelta (count);

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int totalRead = 0;
            var files = manager.Files;

            for (i = 0; i < files.Length; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalRead < count)
            {
                int fileToRead = (int)Math.Min(files[i].Length - offset, count - totalRead);
                fileToRead = Math.Min(fileToRead, Piece.BlockSize);

                if (fileToRead != Writer.Read(files[i], offset, buffer, totalRead, fileToRead))
                    return false;

                offset += fileToRead;
                totalRead += fileToRead;
                if (offset >= files[i].Length)
                {
                    offset = 0;
                    i++;
                }
            }

            return true;
        }

        void Write (ITorrentData manager, long offset, byte [] buffer, int count)
        {
            writeMonitor.AddDelta (count);

            if (offset < 0 || offset + count > manager.Size)
                throw new ArgumentOutOfRangeException("offset");

            int i;
            int totalWritten = 0;
            var files = manager.Files;

            for (i = 0; i < files.Length; i++)
            {
                if (offset < files[i].Length)
                    break;

                offset -= files[i].Length;
            }

            while (totalWritten < count)
            {
                int fileToWrite = (int)Math.Min(files[i].Length - offset, count - totalWritten);
                fileToWrite = Math.Min(fileToWrite, Piece.BlockSize);

                Writer.Write(files[i], offset, buffer, totalWritten, fileToWrite);

                offset += fileToWrite;
                totalWritten += fileToWrite;
                if (offset >= files[i].Length)
                {
                    offset = 0;
                    i++;
                }
            }
        }
    }
}
