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

namespace MonoTorrent.Client
{
    public class DiskManager : IDisposable
    {
        struct BufferedIO
        {
            public TorrentManager manager;
            public long offset;
            public byte [] buffer;
            public int count;
            public TaskCompletionSource<bool> tcs;

            public BufferedIO (TorrentManager manager, long offset, byte [] buffer, int count, TaskCompletionSource<bool> tcs)
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
        readonly ClientEngine engine;
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

        internal PieceWriter Writer { get; set; }

        #endregion Properties


        #region Constructors

        internal DiskManager(ClientEngine engine, PieceWriter writer)
        {
            this.bufferedReads = new Queue<BufferedIO>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.engine = engine;
            this.ReadLimiter = new RateLimiter();
            this.readMonitor = new SpeedMonitor();
            this.writeMonitor = new SpeedMonitor();
            this.WriteLimiter = new RateLimiter();
            this.Writer = writer;

            IOLoop.QueueTimeout (TimeSpan.FromSeconds (1), () => {
                readMonitor.Tick ();
                writeMonitor.Tick ();

                WriteLimiter.UpdateChunks (engine.Settings.MaxWriteRate, WriteRate);
                ReadLimiter.UpdateChunks (engine.Settings.MaxReadRate, ReadRate);

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

        async Task SetError (TorrentManager manager, Reason reason, Exception ex)
        {
            await ClientEngine.MainLoop;
            if (manager.Mode is ErrorMode)
                return;

            manager.Error = new Error (reason, ex);
            manager.Mode = new ErrorMode (manager);
        }

        #endregion

        internal async Task<bool> CheckFileExistsAsync(TorrentManager manager, TorrentFile file)
        {
            await IOLoop;

            try
            {
                return Writer.Exists(file);
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.ReadFailure, ex);
                return true;
            }
        }

        internal async Task<bool> CheckAnyFilesExistAsync(TorrentManager manager)
        {
            await IOLoop;

            try
            {
                for (int i = 0; i < manager.Torrent.Files.Length; i++)
                    if (Writer.Exists(manager.Torrent.Files[i]))
                        return true;
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.ReadFailure, ex);
                return true;
            }
            return false;
        }

        internal async Task FlushAsync()
        {
            await IOLoop;

            foreach (TorrentManager manager in engine.Torrents)
                Writer.Flush(manager.Torrent.Files);
        }

        internal async Task FlushAsync(TorrentManager manager)
        {
            await IOLoop;

            try
            {
                Writer.Flush(manager.Torrent.Files);
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }
        }

        internal async Task FlushAsync(TorrentManager manager, int index)
        {
            await IOLoop;

            try
            {
                foreach (TorrentFile file in manager.Torrent.Files)
                    if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                        Writer.Flush(file);
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }
        }

        internal async Task<byte[]> GetHashAsync(TorrentManager manager, int pieceIndex)
        {
            await IOLoop;

            // We want to be sure we've actually written everything so when we go to hash the
            // piece it will be returned to us in our Read call. If the write were still pending
            // we could accidentally end up reporting the piece was corrupt.
            await WaitForBufferedWrites();

            long startOffset = (long)manager.Torrent.PieceLength * pieceIndex;
            long endOffset = Math.Min(startOffset + manager.Torrent.PieceLength, manager.Torrent.Size);

            byte[] hashBuffer = ClientEngine.BufferManager.GetBuffer(Piece.BlockSize);
            try {
                using (var hasher = HashAlgoFactory.Create<SHA1>()) {
                    hasher.Initialize();

                    while (startOffset != endOffset)
                    {
                        int count = (int)Math.Min(Piece.BlockSize, endOffset - startOffset);
                        if (!await ReadAsync(manager, startOffset, hashBuffer, count).ConfigureAwait(false))
                            return null;
                        startOffset += count;
                        hasher.TransformBlock(hashBuffer, 0, count, hashBuffer, 0);
                    }

                    hasher.TransformFinalBlock(hashBuffer, 0, 0);
                    return hasher.Hash;
                }
            } finally {
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

        internal async Task CloseFilesAsync(TorrentManager manager)
        {
            await IOLoop;

            // Process all pending reads/writes then close any open streams
            try
            {
                this.ProcessBufferedIO(true);
                Writer.Close(manager.Torrent.Files);
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }
        }

        internal async Task MoveFileAsync (TorrentManager manager, TorrentFile file, string newPath)
        {
            await IOLoop;

            try
            {
                newPath = Path.GetFullPath (newPath);
                Writer.Move (file, newPath, false);
                file.FullPath = newPath;
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }
        }

        internal async Task MoveFilesAsync(TorrentManager manager, string newRoot, bool overWriteExisting)
        {
            await IOLoop;

            try
            {
                Writer.Move(newRoot, manager.Torrent.Files, overWriteExisting);
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }
        }

        internal async Task<bool> ReadAsync (TorrentManager manager, long offset, byte [] buffer, int count)
        {
            await IOLoop;

            try
            {
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
            catch (Exception ex)
            {
                await SetError(manager, Reason.ReadFailure, ex);
                return false;
            }
        }

        internal async Task WriteAsync (TorrentManager manager, long offset, byte[] buffer, int count)
        {
            Interlocked.Add(ref bufferedWriteBytes, count);
            await IOLoop;

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
            }
            catch (Exception ex)
            {
                await SetError(manager, Reason.WriteFailure, ex);
            }

            Interlocked.Add(ref bufferedWriteBytes, -count);
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

        bool Read (TorrentManager manager, long offset, byte [] buffer, int count)
        {
            readMonitor.AddDelta (count);
            return Writer.Read (manager.Torrent.Files, offset, buffer, 0, count, manager.Torrent.PieceLength, manager.Torrent.Size);
        }

        void Write (TorrentManager manager, long offset, byte [] buffer, int count)
        {
            writeMonitor.AddDelta (count);
            Writer.Write (manager.Torrent.Files, offset, buffer, 0, count, manager.Torrent.PieceLength, manager.Torrent.Size);
        }
    }
}
