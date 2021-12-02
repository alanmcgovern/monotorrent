
using System;

using MonoTorrent.PieceWriter;

using ReusableTasks;

namespace MonoTorrent.Client
{
    public class NullWriter : IPieceWriter
    {
        public int MaximumOpenFiles { get; }

        public ReusableTask CloseAsync (ITorrentFileInfo file)
        {
            return ReusableTask.CompletedTask;
        }

        public void Dispose ()
        {
        }

        public ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
        {
            return ReusableTask.FromResult (false);
        }

        public ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, Memory<byte> buffer)
        {
            return ReusableTask.FromResult (0);
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public ReusableTask WriteAsync (ITorrentFileInfo file, long offset, ReadOnlyMemory<byte> buffer)
        {
            return ReusableTask.CompletedTask;
        }
    }
}
