using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public delegate void DiskIOCallback(bool successful);

    public partial class DiskManager : IDisposable
    {
        private static readonly MainLoop IOLoop = new MainLoop("Disk IO");

        #region Constructors

        internal DiskManager(ClientEngine engine, PieceWriter writer)
        {
            bufferedReads = new Queue<BufferedIO>();
            bufferedWrites = new Queue<BufferedIO>();
            cache = new Cache<BufferedIO>(true).Synchronize();
            this.engine = engine;
            readLimiter = new RateLimiter();
            readMonitor = new SpeedMonitor();
            writeMonitor = new SpeedMonitor();
            writeLimiter = new RateLimiter();
            Writer = writer;

            LoopTask = delegate
            {
                if (Disposed)
                    return;

                while (bufferedWrites.Count > 0 &&
                       writeLimiter.TryProcess(bufferedWrites.Peek().buffer.Length/2048))
                {
                    BufferedIO write;
                    lock (bufferLock)
                        write = bufferedWrites.Dequeue();
                    try
                    {
                        PerformWrite(write);
                        cache.Enqueue(write);
                    }
                    catch (Exception ex)
                    {
                        if (write.Manager != null)
                            SetError(write.Manager, Reason.WriteFailure, ex);
                    }
                }

                while (bufferedReads.Count > 0 && readLimiter.TryProcess(bufferedReads.Peek().Count/2048))
                {
                    BufferedIO read;
                    lock (bufferLock)
                        read = bufferedReads.Dequeue();

                    try
                    {
                        PerformRead(read);
                        cache.Enqueue(read);
                    }
                    catch (Exception ex)
                    {
                        if (read.Manager != null)
                            SetError(read.Manager, Reason.ReadFailure, ex);
                    }
                }
            };

            IOLoop.QueueTimeout(TimeSpan.FromSeconds(1), delegate
            {
                if (Disposed)
                    return false;

                readMonitor.Tick();
                writeMonitor.Tick();
                LoopTask();
                return true;
            });
        }

        #endregion Constructors

        internal void MoveFile(TorrentManager manager, TorrentFile file, string path)
        {
            IOLoop.QueueWait(delegate
            {
                try
                {
                    path = Path.GetFullPath(path);
                    Writer.Move(file.FullPath, path, false);
                    file.FullPath = path;
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
            });
        }

        internal void MoveFiles(TorrentManager manager, string newRoot, bool overWriteExisting)
        {
            IOLoop.QueueWait(delegate
            {
                try
                {
                    Writer.Move(newRoot, manager.Torrent.Files, overWriteExisting);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
            });
        }

        #region Member Variables

        private readonly object bufferLock = new object();
        private readonly Queue<BufferedIO> bufferedReads;
        private readonly Queue<BufferedIO> bufferedWrites;
        private readonly ICache<BufferedIO> cache;
        private readonly ClientEngine engine;
        private readonly MainLoopTask LoopTask;

        private readonly SpeedMonitor readMonitor;
        private readonly SpeedMonitor writeMonitor;

        internal RateLimiter readLimiter;
        internal RateLimiter writeLimiter;

        #endregion Member Variables

        #region Properties

        public bool Disposed { get; private set; }

        public int QueuedWrites
        {
            get { return bufferedWrites.Count; }
        }

        public int ReadRate
        {
            get { return readMonitor.Rate; }
        }

        public int WriteRate
        {
            get { return writeMonitor.Rate; }
        }

        public long TotalRead
        {
            get { return readMonitor.Total; }
        }

        public long TotalWritten
        {
            get { return writeMonitor.Total; }
        }

        internal PieceWriter Writer { get; set; }

        #endregion Properties

        #region Methods

        internal WaitHandle CloseFileStreams(TorrentManager manager)
        {
            var handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate
            {
                // Process all pending reads/writes then close any open streams
                try
                {
                    LoopTask();
                    Writer.Close(manager.Torrent.Files);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
                finally
                {
                    handle.Set();
                }
            });

            return handle;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;
            // FIXME: Ensure everything is written to disk before killing the mainloop.
            IOLoop.QueueWait((MainLoopTask) Writer.Dispose);
        }

        public void Flush()
        {
            IOLoop.QueueWait(delegate
            {
                foreach (var manager in engine.Torrents)
                    Writer.Flush(manager.Torrent.Files);
            });
        }

        public void Flush(TorrentManager manager)
        {
            Check.Manager(manager);
            IOLoop.QueueWait(delegate { Writer.Flush(manager.Torrent.Files); });
        }

        private void PerformWrite(BufferedIO io)
        {
            // Find the block that this data belongs to and set it's state to "Written"
            var index = io.PieceOffset/Piece.BlockSize;
            try
            {
                // Perform the actual write
                Writer.Write(io.Files, io.Offset, io.buffer, 0, io.Count, io.PieceLength, io.Manager.Torrent.Size);
                writeMonitor.AddDelta(io.Count);
            }
            finally
            {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(true);
            }
        }

        private void PerformRead(BufferedIO io)
        {
            try
            {
                if (Writer.Read(io.Files, io.Offset, io.buffer, 0, io.Count, io.PieceLength, io.Manager.Torrent.Size))
                    io.ActualCount = io.Count;
                else
                    io.ActualCount = 0;
                readMonitor.AddDelta(io.ActualCount);
            }
            finally
            {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(io.ActualCount == io.Count);
            }
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(delegate
            {
                try
                {
                    foreach (var file in manager.Torrent.Files)
                        if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                            Writer.Flush(file);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
            });
        }

        internal void QueueRead(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            var io = cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueRead(io, callback);
        }

        private void QueueRead(BufferedIO io, DiskIOCallback callback)
        {
            io.Callback = callback;
            if (Thread.CurrentThread == IOLoop.thread)
            {
                PerformRead(io);
                cache.Enqueue(io);
            }
            else
                lock (bufferLock)
                {
                    bufferedReads.Enqueue(io);
                    if (bufferedReads.Count == 1)
                        IOLoop.Queue(LoopTask);
                }
        }

        internal void QueueWrite(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            var io = cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueWrite(io, callback);
        }

        private void QueueWrite(BufferedIO io, DiskIOCallback callback)
        {
            io.Callback = callback;
            if (Thread.CurrentThread == IOLoop.thread)
            {
                PerformWrite(io);
                cache.Enqueue(io);
            }
            else
                lock (bufferLock)
                {
                    bufferedWrites.Enqueue(io);
                    if (bufferedWrites.Count == 1)
                        IOLoop.Queue(LoopTask);
                }
        }

        internal bool CheckAnyFilesExist(TorrentManager manager)
        {
            var result = false;
            IOLoop.QueueWait(delegate
            {
                try
                {
                    for (var i = 0; i < manager.Torrent.Files.Length && !result; i++)
                        result = Writer.Exists(manager.Torrent.Files[i]);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.ReadFailure, ex);
                }
            });
            return result;
        }

        internal bool CheckFileExists(TorrentManager manager, TorrentFile file)
        {
            var result = false;
            IOLoop.QueueWait(delegate
            {
                try
                {
                    result = Writer.Exists(file);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.ReadFailure, ex);
                }
            });
            return result;
        }

        private void SetError(TorrentManager manager, Reason reason, Exception ex)
        {
            ClientEngine.MainLoop.Queue(delegate
            {
                if (manager.Mode is ErrorMode)
                    return;

                manager.Error = new Error(reason, ex);
                manager.Mode = new ErrorMode(manager);
            });
        }

        internal void BeginGetHash(TorrentManager manager, int pieceIndex, MainLoopResult callback)
        {
            var count = 0;
            var offset = (long) manager.Torrent.PieceLength*pieceIndex;
            var endOffset = Math.Min(offset + manager.Torrent.PieceLength, manager.Torrent.Size);

            var hashBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);

            var hasher = HashAlgoFactory.Create<SHA1>();
            hasher.Initialize();

            DiskIOCallback readCallback = null;
            readCallback = delegate(bool successful)
            {
                if (successful)
                    hasher.TransformBlock(hashBuffer, 0, count, hashBuffer, 0);
                offset += count;

                if (!successful || offset == endOffset)
                {
                    object hash = null;
                    if (successful)
                    {
                        hasher.TransformFinalBlock(hashBuffer, 0, 0);
                        hash = hasher.Hash;
                    }
                    ((IDisposable) hasher).Dispose();
                    ClientEngine.BufferManager.FreeBuffer(ref hashBuffer);
                    ClientEngine.MainLoop.Queue(delegate { callback(hash); });
                }
                else
                {
                    count = (int) Math.Min(Piece.BlockSize, endOffset - offset);
                    QueueRead(manager, offset, hashBuffer, count, readCallback);
                }
            };

            count = (int) Math.Min(Piece.BlockSize, endOffset - offset);
            QueueRead(manager, offset, hashBuffer, count, readCallback);
        }

        #endregion
    }
}