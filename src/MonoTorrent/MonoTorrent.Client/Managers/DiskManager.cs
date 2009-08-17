using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent.Client.Managers
{
    public class DiskManager : IDisposable
    {
        private static MainLoop IOLoop = new MainLoop("Disk IO");

        #region Member Variables

        private object bufferLock = new object();
        private Queue<BufferedIO> bufferedReads;
        private Queue<BufferedIO> bufferedWrites;
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

        internal WaitHandle CloseFileStreams(TorrentManager manager, string path, TorrentFile[] files)
        {
            ManualResetEvent handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate {
				// Process all pending reads/writes then close any open streams
				try
				{
					LoopTask();
					writer.Close(path, files);
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
                    writer.Flush(manager.SavePath, manager.Torrent.Files);
            });
        }

        public void Flush(TorrentManager manager)
        {
            Check.Manager(manager);
            IOLoop.QueueWait(delegate {
                writer.Flush(manager.SavePath, manager.Torrent.Files);
            });
        }

        private void PerformWrite(BufferedIO data)
        {
            PeerId id = data.Id;
            Piece piece = data.Piece;

            // Find the block that this data belongs to and set it's state to "Written"
            int index = data.PieceOffset / Piece.BlockSize;

            // Perform the actual write
            writer.Write(data);
            writeMonitor.AddDelta(data.Count);

            piece.Blocks[index].Written = true;

            data.Complete = true;
            if (data.Callback != null)
                data.Callback();
        }

        private void PerformRead(BufferedIO io)
        {
            io.ActualCount = writer.ReadChunk(io);
            readMonitor.AddDelta(io.ActualCount);

            io.Complete = true;
			if (io.Callback != null)
				io.Callback();
        }

        internal int Read(TorrentManager manager, byte[] buffer, int bufferOffset, long pieceStartIndex, int bytesToRead)
        {
            string path = manager.SavePath;
            ArraySegment<byte> b = new ArraySegment<byte>(buffer, bufferOffset, bytesToRead);
            BufferedIO io = new BufferedIO(manager, b, pieceStartIndex, bytesToRead, manager.Torrent.PieceLength, manager.Torrent.Files, path);
            IOLoop.QueueWait((MainLoopTask)delegate {
                PerformRead(io);
            });
            return io.ActualCount;
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(delegate {
                foreach (TorrentFile file in manager.Torrent.Files)
                    if (file.StartPieceIndex >= index && file.EndPieceIndex <= index)
                        writer.Flush(manager.SavePath, file);
            });
        }

		//internal void QueueRead(BufferedIO io)
		//{
		//    QueueRead(io, null);
		//}

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

		//internal void QueueWrite(BufferedIO io)
		//{
		//    QueueWrite(io, null);
		//}

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
                    result = writer.Exists(manager.SavePath, manager.Torrent.Files);
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
            if (manager.Mode is ErrorMode)
                return;

            manager.Error = new Error(Reason.ReadFailure, ex);
            manager.Mode = new ErrorMode(manager);
        }
		
		internal void BeginGetHash(TorrentManager manager, int pieceIndex, MainLoopResult callback)
		{
			long fileSize = manager.Torrent.Size;
			int pieceLength = manager.Torrent.PieceLength;
			int bytesToRead = 0;
			long pieceStartIndex = (long)pieceLength * pieceIndex;
			BufferedIO io = null;
			ArraySegment<byte> hashBuffer = BufferManager.EmptyBuffer;
			List<BufferedIO> list = new List<BufferedIO>();

			MainLoopTask readCallback = delegate {
				for (int i = 0; i < list.Count; i++)
				{
					if (!list[i].Complete)
						return;
				}
				ClientEngine.MainLoop.Queue(delegate
				{
					using (SHA1 hasher = HashAlgoFactory.Create<SHA1>()) {
						hasher.Initialize();
						for (int i = 0; i < list.Count; i++)
						{
							hashBuffer = list[i].buffer;
							hasher.TransformBlock(hashBuffer.Array, hashBuffer.Offset, list[i].ActualCount, hashBuffer.Array, hashBuffer.Offset);
							ClientEngine.BufferManager.FreeBuffer(ref list[i].buffer);
						}
						hasher.TransformFinalBlock(hashBuffer.Array, hashBuffer.Offset, 0);
						callback(hasher.Hash);
					}
				});
			};

			for (long i = pieceStartIndex; i < (pieceStartIndex + pieceLength); i += Piece.BlockSize)
			{
				hashBuffer = BufferManager.EmptyBuffer;
				ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);
				bytesToRead = Piece.BlockSize;
				if ((i + bytesToRead) > fileSize)
					bytesToRead = (int)(fileSize - i);

				io = new BufferedIO(manager, hashBuffer, i, bytesToRead, manager.Torrent.PieceLength, manager.Torrent.Files, manager.SavePath);
				list.Add(io);

				if (bytesToRead != Piece.BlockSize)
					break;
			}

			for (int i=0; i < list.Count; i++)
				manager.Engine.DiskManager.QueueRead(list[i], readCallback);
		}

        #endregion

        internal void MoveFiles(TorrentManager torrentManager, string oldPath, string newPath, bool overWriteExisting)
        {
            IOLoop.QueueWait(delegate {
                    writer.Move(oldPath, newPath, torrentManager.Torrent.Files, overWriteExisting);
            });
        }
    }
}
