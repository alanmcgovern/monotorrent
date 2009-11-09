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
    public class DiskManager : IDisposable
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

                while (this.bufferedWrites.Count > 0 && writeLimiter.TryProcess(bufferedWrites.Peek ().buffer.Count / 2048))
                {
                    BufferedIO write;
                    lock (bufferLock)
                        write = this.bufferedWrites.Dequeue();
                    try
                    {
                        PerformWrite(write);
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

        internal WaitHandle CloseFileStreams(TorrentManager manager, TorrentFile[] files)
        {
            ManualResetEvent handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate {
				// Process all pending reads/writes then close any open streams
				try
				{
					LoopTask();
					writer.Close(files);
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
            Piece piece = io.Piece;

            // Find the block that this data belongs to and set it's state to "Written"
            int index = io.PieceOffset / Piece.BlockSize;

            // Perform the actual write
            writer.Write(io.Files, io.Offset, io.buffer.Array, io.buffer.Offset, io.Count, io.PieceLength, io.Manager.Torrent.Size);
            writeMonitor.AddDelta(io.Count);

            piece.Blocks[index].Written = true;

            io.Complete = true;
            if (io.Callback != null)
                io.Callback();
        }

        private void PerformRead(BufferedIO io)
        {
            if (writer.Read(io.Files, io.Offset, io.buffer.Array, io.buffer.Offset, io.Count, io.PieceLength, io.Manager.Torrent.Size))
                io.ActualCount = io.Count;
            else
                io.ActualCount = 0;

            io.Complete = true;
            if (io.Callback != null)
                io.Callback();
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(delegate {
                foreach (TorrentFile file in manager.Torrent.Files)
                    if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                        writer.Flush(file);
            });
        }

		internal void QueueRead(BufferedIO io, MainLoopTask callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread)
				PerformRead(io);
			else
				lock (bufferLock)
				{
					bufferedReads.Enqueue(io);
					DiskManager.IOLoop.Queue(LoopTask);
				}
		}

		internal void QueueWrite(BufferedIO io, MainLoopTask callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread)
				PerformWrite(io);
			else
				lock (bufferLock)
				{
					bufferedWrites.Enqueue(io);
					DiskManager.IOLoop.Queue(LoopTask);
				}
		}

        internal bool CheckFilesExist(TorrentManager manager)
        {
            bool result = false;
            IOLoop.QueueWait(delegate {
                try
                {
                    result = writer.Exists(manager.Torrent.Files);
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
			long fileSize = manager.Torrent.Size;
			int pieceLength = manager.Torrent.PieceLength;
			int bytesToRead = 0;
			long pieceStartIndex = (long)pieceLength * pieceIndex;
			BufferedIO io = null;
			ArraySegment<byte> hashBuffer = BufferManager.EmptyBuffer;

			MainLoopTask readCallback = delegate {
				ClientEngine.MainLoop.Queue(delegate
				{
					using (SHA1 hasher = HashAlgoFactory.Create<SHA1>()) {
						hasher.Initialize();
					    hashBuffer = io.buffer;
						hasher.TransformBlock(hashBuffer.Array, hashBuffer.Offset, io.ActualCount, hashBuffer.Array, hashBuffer.Offset);
						ClientEngine.BufferManager.FreeBuffer(ref io.buffer);
						hasher.TransformFinalBlock(hashBuffer.Array, hashBuffer.Offset, 0);
                        cache.Enqueue(io);
						callback(hasher.Hash);
					}
				});
			};

			for (long i = pieceStartIndex; i < (pieceStartIndex + pieceLength); i += pieceLength)
			{
				hashBuffer = BufferManager.EmptyBuffer;
				ClientEngine.BufferManager.GetBuffer(ref hashBuffer, pieceLength);
				bytesToRead = pieceLength;
				if ((i + bytesToRead) > fileSize)
					bytesToRead = (int)(fileSize - i);

				io = cache.Dequeue();
				io.Initialise(manager, hashBuffer, i, bytesToRead, manager.Torrent.PieceLength, manager.Torrent.Files);

                if (bytesToRead != pieceLength)
					break;
			}

				manager.Engine.DiskManager.QueueRead(io, readCallback);
		}

        #endregion

        internal void MoveFiles(TorrentManager torrentManager, string newRoot, bool overWriteExisting)
        {
            IOLoop.QueueWait(delegate {
                writer.Move (newRoot, torrentManager.Torrent.Files, overWriteExisting);
            });
        }


        internal void QueueRead(TorrentManager manager, long offset, ArraySegment<byte> buffer, int count, MainLoopTask callback)
        {
            BufferedIO io = cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueRead(io, callback);
        }
    }
}
