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
        public BlockingCollection<(ITorrentFileInfo file, string fullPath, bool overwrite, ReusableTaskCompletionSource<object> tcs)> Moves = new BlockingCollection<(ITorrentFileInfo file, string fullPath, bool overwrite, ReusableTaskCompletionSource<object> tcs)> ();
        public BlockingCollection<(ITorrentFileInfo file, long offset, Memory<byte> buffer, ReusableTaskCompletionSource<int> tcs)> Reads = new BlockingCollection<(ITorrentFileInfo file, long offset, Memory<byte> buffer, ReusableTaskCompletionSource<int> tcs)> ();
        public BlockingCollection<(ITorrentFileInfo file, long offset, ReadOnlyMemory<byte> buffer, ReusableTaskCompletionSource<object> tcs)> Writes = new BlockingCollection<(ITorrentFileInfo file, long offset, ReadOnlyMemory<byte> buffer, ReusableTaskCompletionSource<object> tcs)> ();

        public int MaximumOpenFiles => 0;

        public async ReusableTask CloseAsync (ITorrentFileInfo file)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Closes.Add ((file, tcs));
            await tcs.Task;
        }

        public void Dispose ()
        {
        }

        public async ReusableTask<bool> ExistsAsync (ITorrentFileInfo file)
        {
            var tcs = new ReusableTaskCompletionSource<bool> ();
            Exists.Add ((file, tcs));
            return await tcs.Task;
        }

        public async ReusableTask FlushAsync (ITorrentFileInfo file)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Flushes.Add ((file, tcs));
            await tcs.Task;
        }
        public async ReusableTask MoveAsync (ITorrentFileInfo file, string fullPath, bool overwrite)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Moves.Add ((file, fullPath, overwrite, tcs));
            await tcs.Task;
        }

        public async ReusableTask<int> ReadAsync (ITorrentFileInfo file, long offset, Memory<byte> buffer)
        {
            var tcs = new ReusableTaskCompletionSource<int> ();
            Reads.Add ((file, offset, buffer, tcs));
            return await tcs.Task;
        }

        public ReusableTask SetMaximumOpenFilesAsync (int maximumOpenFiles)
        {
            return ReusableTask.CompletedTask;
        }

        public async ReusableTask WriteAsync (ITorrentFileInfo file, long offset, ReadOnlyMemory<byte> buffer)
        {
            var tcs = new ReusableTaskCompletionSource<object> ();
            Writes.Add ((file, offset, buffer, tcs));
            await tcs.Task;
        }
    }
}
