using System;
using System.Collections.Concurrent;

using MonoTorrent.PieceWriter;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class BlockingWriter : IPieceWriter
    {
        public BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<object> tcs)> Closes = new BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<object> tcs)> ();
        public BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<bool> tcs)> Exists = new BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<bool> tcs)> ();
        public BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<object> tcs)> Flushes = new BlockingCollection<(ITorrentFile file, ReusableTaskCompletionSource<object> tcs)> ();
        public BlockingCollection<(ITorrentManagerFile file, string fullPath, bool overwrite, ReusableTaskCompletionSource<object> tcs)> Moves = new BlockingCollection<(ITorrentManagerFile file, string fullPath, bool overwrite, ReusableTaskCompletionSource<object> tcs)> ();
        public BlockingCollection<(ITorrentManagerFile file, long offset, Memory<byte> buffer, ReusableTaskCompletionSource<int> tcs)> Reads = new BlockingCollection<(ITorrentManagerFile file, long offset, Memory<byte> buffer, ReusableTaskCompletionSource<int> tcs)> ();
        public BlockingCollection<(ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer, ReusableTaskCompletionSource<object> tcs)> Writes = new BlockingCollection<(ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer, ReusableTaskCompletionSource<object> tcs)> ();

        public int OpenFiles => 0;
        public int MaximumOpenFiles => 0;

        public async ReusableTask CloseAsync (ITorrentManagerFile file)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Closes.Add ((file, tcs));
            await tcs.Task;
        }

        public ReusableTask<bool> CreateAsync (ITorrentManagerFile file, FileCreationOptions options)
        {
            throw new NotImplementedException ();
        }

        public void Dispose ()
        {
        }

        public async ReusableTask<bool> ExistsAsync (ITorrentManagerFile file)
        {
            var tcs = new ReusableTaskCompletionSource<bool> ();
            Exists.Add ((file, tcs));
            return await tcs.Task;
        }

        public async ReusableTask FlushAsync (ITorrentManagerFile file)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Flushes.Add ((file, tcs));
            await tcs.Task;
        }

        public ReusableTask<long?> GetLengthAsync (ITorrentManagerFile file)
        {
            throw new NotImplementedException ();
        }

        public async ReusableTask MoveAsync (ITorrentManagerFile file, string fullPath, bool overwrite)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Moves.Add ((file, fullPath, overwrite, tcs));
            await tcs.Task;
        }

        public async ReusableTask<int> ReadAsync (ITorrentManagerFile file, long offset, Memory<byte> buffer)
        {
            var tcs = new ReusableTaskCompletionSource<int> ();
            Reads.Add ((file, offset, buffer, tcs));
            return await tcs.Task;
        }

        public ReusableTask<bool> SetLengthAsync (ITorrentManagerFile file, long length)
        {
            throw new NotImplementedException ();
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public async ReusableTask WriteAsync (ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Writes.Add ((file, offset, buffer, tcs));
            await tcs.Task;
        }
    }
}
