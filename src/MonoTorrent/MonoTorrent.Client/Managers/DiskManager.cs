using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent.Client
{
    public delegate void DiskIOCallback (bool successful);

    public partial class DiskManager : IDisposable
    {
        private static MainLoop IOLoop = new MainLoop("Disk IO");
        #region Member Variables

        private object bufferLock = new object();
        private Queue<BufferedIO> bufferedReads;
        private Queue<BufferedIO> bufferedWrites;
        private ICache<BufferedIO> cache;
        private bool disposed;
        private ClientEngine engine;
        private MainLoopTask LoopTask;

        private SpeedMonitor readMonitor;
        private SpeedMonitor writeMonitor;

        internal RateLimiter readLimiter;
        internal RateLimiter writeLimiter;
        private PieceWriter writer;

        #endregion Member Variables


        #region Properties

        public bool Disposed
        {
            get { return disposed; }
        }

        public int QueuedWrites
        {
            get { return this.bufferedWrites.Count; }
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

        internal PieceWriter Writer
        {
            get { return writer; }
            set { writer = value; }
        }

        #endregion Properties


        #region Constructors

        internal DiskManager(ClientEngine engine, PieceWriter writer)
        {
            this.bufferedReads = new Queue<BufferedIO>();
            this.bufferedWrites = new Queue<BufferedIO>();
            this.cache = new Cache<BufferedIO>(true).Synchronize ();
            this.engine = engine;
            this.readLimiter = new RateLimiter();
            this.readMonitor = new SpeedMonitor();
            this.writeMonitor = new SpeedMonitor();
            this.writeLimiter = new RateLimiter();
            this.writer = writer;

            LoopTask = delegate {
                if (disposed)
                    return;

                while (this.bufferedWrites.Count > 0 && writeLimiter.TryProcess(bufferedWrites.Peek ().buffer.Length / 2048))
                {
                    BufferedIO write;
                    lock (bufferLock)
                        write = this.bufferedWrites.Dequeue();
                    try
                    {
                        PerformWrite(write);
                        cache.Enqueue (write);
                    }
                    catch (Exception ex)
                    {
                        if (write.Manager != null)
                            SetError(write.Manager, Reason.WriteFailure, ex);
                    }
                }

                while (this.bufferedReads.Count > 0 && readLimiter.TryProcess(bufferedReads.Peek().Count / 2048))
                {
                    BufferedIO read;
                    lock(bufferLock)
                        read = this.bufferedReads.Dequeue();

                    try
                    {
                        PerformRead(read);
                        cache.Enqueue (read);
                    }
                    catch (Exception ex)
                    {
                        if(read.Manager != null)
                            SetError(read.Manager, Reason.ReadFailure, ex);
                    }
                }
            };

            IOLoop.QueueTimeout(TimeSpan.FromSeconds(1), delegate {
                if (disposed)
                    return false;

                readMonitor.Tick();
                writeMonitor.Tick();
                LoopTask();
                return true;
            });
        }

        #endregion Constructors


        #region Methods

        internal WaitHandle CloseFileStreams(TorrentManager manager)
        {
            ManualResetEvent handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate {
				// Process all pending reads/writes then close any open streams
				try
				{
					LoopTask();
					writer.Close(manager.Torrent.Files);
				}
                catch (Exception ex)
                {
                    SetError (manager, Reason.WriteFailure, ex);
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
            if (disposed)
                return;

            disposed = true;
            // FIXME: Ensure everything is written to disk before killing the mainloop.
            IOLoop.QueueWait((MainLoopTask)writer.Dispose);
        }

        public void Flush()
        {
            IOLoop.QueueWait(delegate {
                foreach (TorrentManager manager in engine.Torrents)
                    writer.Flush(manager.Torrent.Files);
            });
        }

        public void Flush(TorrentManager manager)
        {
            Check.Manager(manager);
            IOLoop.QueueWait(delegate {
                writer.Flush(manager.Torrent.Files);
            });
        }

        private void PerformWrite(BufferedIO io)
        {
            // Find the block that this data belongs to and set it's state to "Written"
            int index = io.PieceOffset / Piece.BlockSize;
            try {
                // Perform the actual write
                writer.Write(io.Files, io.Offset, io.buffer, 0, io.Count, io.PieceLength, io.Manager.Torrent.Size);
                writeMonitor.AddDelta(io.Count);
            } finally {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(true);
            }
        }

        private void PerformRead(BufferedIO io)
        {
            try {
                if (writer.Read(io.Files, io.Offset, io.buffer, 0, io.Count, io.PieceLength, io.Manager.Torrent.Size))
                    io.ActualCount = io.Count;
                else
                    io.ActualCount = 0;
                readMonitor.AddDelta(io.ActualCount);
            } finally {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(io.ActualCount == io.Count);
            }
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(delegate {
                try
                {
                    foreach (TorrentFile file in manager.Torrent.Files)
                        if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                            writer.Flush(file);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
            });
        }

        internal void QueueRead(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            BufferedIO io = cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueRead(io, callback);
        }

		void QueueRead(BufferedIO io, DiskIOCallback callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread) {
				PerformRead(io);
				cache.Enqueue (io);
			}
			else
				lock (bufferLock)
				{
					bufferedReads.Enqueue(io);
                    if (bufferedReads.Count == 1)
                        DiskManager.IOLoop.Queue(LoopTask);
				}
		}

        internal void QueueWrite(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            BufferedIO io = cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueWrite(io, callback);
        }

		void QueueWrite(BufferedIO io, DiskIOCallback callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread) {
				PerformWrite(io);
				cache.Enqueue (io);
			}
			else
				lock (bufferLock)
				{
					bufferedWrites.Enqueue(io);
                    if (bufferedWrites.Count == 1)
                        DiskManager.IOLoop.Queue(LoopTask);
				}
		}

        internal bool CheckAnyFilesExist(TorrentManager manager)
        {
            bool result = false;
            IOLoop.QueueWait(delegate {
                try
                {
                    for (int i = 0; i < manager.Torrent.Files.Length && !result; i++)
                        result = writer.Exists (manager.Torrent.Files [i]);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.ReadFailure, ex);
                }
            });
            return result;
        }

        internal bool CheckFileExists (TorrentManager manager, TorrentFile file)
        {
            bool result = false;
            IOLoop.QueueWait(delegate {
                try
                {
                    result = writer.Exists (file);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.ReadFailure, ex);
                }
            });
            return result;
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

        internal void BeginGetHash(TorrentManager manager, int pieceIndex, MainLoopResult callback)
        {
            int count = 0;
            long offset = (long) manager.Torrent.PieceLength * pieceIndex;
            long endOffset = Math.Min(offset + manager.Torrent.PieceLength, manager.Torrent.Size);

            byte[] hashBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);

            SHA1 hasher = HashAlgoFactory.Create<SHA1>();
            hasher.Initialize();

            DiskIOCallback readCallback = null;
            readCallback = delegate(bool successful) {
                
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
                    ((IDisposable)hasher).Dispose();
                    ClientEngine.BufferManager.FreeBuffer(ref hashBuffer);
                    ClientEngine.MainLoop.Queue(delegate {
                        callback(hash);
                    });
                }
                else
                {
                    count = (int)Math.Min(Piece.BlockSize, endOffset - offset);
                    QueueRead(manager, offset, hashBuffer, count, readCallback);
                }
            };

            count = (int)Math.Min(Piece.BlockSize, endOffset - offset);
            QueueRead(manager, offset, hashBuffer, count, readCallback);
        }

        #endregion

        internal void MoveFile (TorrentManager manager, TorrentFile file, string path)
        {
            IOLoop.QueueWait (delegate {
                try
                {
                    path = Path.GetFullPath (path);
                    writer.Move (file.FullPath, path, false);
                    file.FullPath = path;
                }
                catch (Exception ex)
                {
                    SetError (manager, Reason.WriteFailure, ex);
                }
            });
        }

        internal void MoveFiles(TorrentManager manager, string newRoot, bool overWriteExisting)
        {
            IOLoop.QueueWait(delegate {
                try
                {
                    writer.Move(newRoot, manager.Torrent.Files, overWriteExisting);
                }
                catch (Exception ex)
                {
                    SetError(manager, Reason.WriteFailure, ex);
                }
            });
        }
    }
}
