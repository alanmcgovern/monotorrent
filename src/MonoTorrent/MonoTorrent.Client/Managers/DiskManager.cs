using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriters;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    public class DiskManager : IDisposable
    {
        public struct BufferedIO
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

        internal RateLimiter ReadLimiter;
        internal RateLimiter WriteLimiter;

        #endregion Member Variables


        #region Properties

        public bool Disposed { get; private set; }

        public int BufferedWriteBytes => bufferedWriteBytes;

        public int ReadRate => readMonitor.Rate;
        public int WriteRate => writeMonitor.Rate;

        public long TotalRead => readMonitor.Total;
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

        public void Dispose()
        {
            if (Disposed)
                return;

            IOLoop.QueueWait (() => {
                ProcessBufferedIO (true);
                Writer.Dispose ();
                Disposed = true;
            });
        }

        void SetError (TorrentManager manager, Reason reason, Exception ex)
        {
            ClientEngine.MainLoop.Queue (delegate {
                if (manager.Mode is ErrorMode)
                    return;

                manager.Error = new Error (reason, ex);
                manager.Mode = new ErrorMode (manager);
            });
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
                SetError(manager, Reason.ReadFailure, ex);
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
                SetError(manager, Reason.ReadFailure, ex);
                return true;
            }
            return false;
        }

        public async Task FlushAsync()
        {
            await IOLoop;

            foreach (TorrentManager manager in engine.Torrents)
                Writer.Flush(manager.Torrent.Files);
        }

        public async Task FlushAsync(TorrentManager manager)
        {
            await IOLoop;

            try
            {
                Writer.Flush(manager.Torrent.Files);
            }
            catch (Exception ex)
            {
                SetError(manager, Reason.WriteFailure, ex);
            }
        }

        public async Task FlushAsync(TorrentManager manager, int index)
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
                SetError(manager, Reason.WriteFailure, ex);
            }
        }

        public async Task<byte[]> GetHashAsync(TorrentManager manager, int pieceIndex)
        {
            await IOLoop;

            // We want to be sure we've actually written everything so when we go to hash the
            // piece it will be returned to us in our Read call. If the write were still pending
            // we could accidentally end up reporting the piece was corrupt.
            await WaitForBufferedWrites();

            long startOffset = (long)manager.Torrent.PieceLength * pieceIndex;
            long endOffset = Math.Min(startOffset + manager.Torrent.PieceLength, manager.Torrent.Size);

            byte[] hashBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);

            SHA1 hasher = HashAlgoFactory.Create<SHA1>();
            hasher.Initialize();

            while (startOffset != endOffset)
            {
                int count = (int)Math.Min(Piece.BlockSize, endOffset - startOffset);
                if (!await ReadAsync(manager, startOffset, hashBuffer, count).ConfigureAwait(false))
                {
                    ClientEngine.BufferManager.FreeBuffer(ref hashBuffer);
                    return null;
                }
                startOffset += count;
                hasher.TransformBlock(hashBuffer, 0, count, hashBuffer, 0);
            }

            hasher.TransformFinalBlock(hashBuffer, 0, 0);
            ClientEngine.BufferManager.FreeBuffer(ref hashBuffer);
            return hasher.Hash;
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

        public async Task CloseFilesAsync(TorrentManager manager)
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
                SetError(manager, Reason.WriteFailure, ex);
            }
        }

        public async Task MoveFileAsync (TorrentManager manager, TorrentFile file, string path)
        {
            await IOLoop;

            try
            {
                path = Path.GetFullPath (path);
                Writer.Move (file.FullPath, path, false);
                file.FullPath = path;
            }
            catch (Exception ex)
            {
                SetError (manager, Reason.WriteFailure, ex);
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
                SetError(manager, Reason.WriteFailure, ex);
            }
        }

        public async Task<bool> ReadAsync (TorrentManager manager, long offset, byte [] buffer, int count)
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
                SetError(manager, Reason.ReadFailure, ex);
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
                SetError(manager, Reason.WriteFailure, ex);
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
