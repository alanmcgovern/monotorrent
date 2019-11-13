using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;

using ReusableTasks;

namespace MonoTorrent
{
    class TorrentCreatorWorker
    {
        public TimeSpan ReadAllData_DequeueBufferTime;
        public TimeSpan ReadAllData_EnqueueFilledBufferTime;
        public TimeSpan ReadAllData_ReadTime;

        public TimeSpan Hashing_DequeueFilledTime;
        public TimeSpan Hashing_HashingTime;
        public TimeSpan Hashing_EnqueueEmptyTime;

        List<TorrentFile> Files { get; }
        long PieceStart { get; }
        long PieceCount { get; }
        long PieceLength { get; }
        IPieceWriter Writer { get; }

        public TorrentCreatorWorker (long pieceStart, long pieceCount, long pieceLength, List<TorrentFile> files, IPieceWriter writer)
        {
            PieceStart = pieceStart;
            PieceCount = pieceCount;
            PieceLength = pieceLength;
            Files = files;
            Writer = writer;
        }

        public async ReusableTask CalculateHashes (byte[] torrentHashes, CancellationToken token)
        {
            await MainLoop.SwitchToThreadpool ();

            // We should be hashing Block N, have Block N + 1 in memory and be reading Block N + 2.
            var emptyBuffers = new AsyncProducerConsumerQueue<byte[]> (8);
            var filledBuffers = new AsyncProducerConsumerQueue<(byte[], int, int)> (8);

            for (int i = 0; i < emptyBuffers.Capacity; i ++)
                await emptyBuffers.EnqueueAsync (new byte[TorrentCreator.BlockSize]);

            using var shaHasher = HashAlgoFactory.Create<SHA1> ();

            using var emptyBuffersDisposal = token.Register (emptyBuffers.CompleteAdding);
            using var filledBuffersDisposal = token.Register (filledBuffers.CompleteAdding);

            var readTask = ReadAllData (emptyBuffers, filledBuffers).AsTask ();
            _ = readTask.ContinueWith (t => {
                if (t.IsFaulted) {
                    emptyBuffers.CompleteAdding ();
                    filledBuffers.CompleteAdding ();
                }
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted | System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously);

            var timer = ValueStopwatch.StartNew ();
            shaHasher.Initialize ();
            while (true) {
                byte[] buffer;
                int read;
                int pieceIndex;

                try {
                    timer.Restart ();
                    (buffer, read, pieceIndex) = await filledBuffers.DequeueAsync ();
                    Hashing_DequeueFilledTime += timer.Elapsed;
                } catch (InvalidOperationException) {
                    // We got this probably because we're all done. Check for cancellation first, and
                    // if we haven't been cancelled let's gracefully bail out!
                    token.ThrowIfCancellationRequested ();
                    break;
                }

                // The piece has been fully read.
                if (buffer == null) {
                    shaHasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                    Array.Copy (shaHasher.Hash, 0, torrentHashes, pieceIndex * 20, shaHasher.Hash.Length);
                    shaHasher.Initialize ();
                } else {
                    timer.Restart ();
                    shaHasher?.TransformBlock (buffer, 0, read, buffer, 0);
                    Hashing_HashingTime += timer.Elapsed;

                    timer.Restart ();
                    await emptyBuffers.EnqueueAsync (buffer);
                    Hashing_EnqueueEmptyTime += timer.Elapsed;
                }
            }

            await readTask;
        }

        async ReusableTask ReadAllData (AsyncProducerConsumerQueue<byte[]> emptyBuffers, AsyncProducerConsumerQueue<(byte[], int, int)> filledBuffers)
        {
            await MainLoop.SwitchToThreadpool ();

            var timer = ValueStopwatch.StartNew ();

            using var stream = new FileStream (Files[0].FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, FileOptions.SequentialScan);

            stream.Seek (PieceStart * PieceLength, SeekOrigin.Begin);
            var totalLength = Files.Sum (t => t.Length);
            for (int i = 0; i < PieceCount; i ++) {
                long torrentOffset = (PieceStart + i) * PieceLength;
                long availableToRead = (int) Math.Min (PieceLength, totalLength - torrentOffset);
                int read = 0;
                while (availableToRead > 0) {
                    timer.Restart ();
                    var buffer = await emptyBuffers.DequeueAsync ().ConfigureAwait (false);
                    ReadAllData_DequeueBufferTime += timer.Elapsed;

                    timer.Restart ();
                    var count = (int) Math.Min (buffer.Length, availableToRead);
                    if (stream.Position != torrentOffset + read)
                        stream.Seek (torrentOffset + read, SeekOrigin.Begin);
                    if (stream.Read (buffer, 0 , count) != count)
                        throw new IOException ("Could not read requested data");
                    availableToRead -= count;
                    read += count;
                    ReadAllData_ReadTime += timer.Elapsed;

                    timer.Restart ();
                    await filledBuffers.EnqueueAsync ((buffer, count, (int)(torrentOffset / PieceLength))).ConfigureAwait (false);
                    ReadAllData_EnqueueFilledBufferTime += timer.Elapsed;
                }

                timer.Restart ();
                await filledBuffers.EnqueueAsync ((null, 0, (int)(torrentOffset / PieceLength))).ConfigureAwait (false);
                ReadAllData_EnqueueFilledBufferTime += timer.Elapsed;
            }
            filledBuffers.CompleteAdding ();
        }

        bool Read (byte[] buffer, long torrentOffset, int count)
        {
            int i;
            int totalRead = 0;
            var files = Files;

            for (i = 0; i < files.Count; i++)
            {
                if (torrentOffset < files[i].Length)
                    break;

                torrentOffset -= files[i].Length;
            }

            while (totalRead < count)
            {
                int fileToRead = (int)Math.Min(files[i].Length - torrentOffset, count - totalRead);

                lock (Writer)
                    if (fileToRead != Writer.Read(files[i], torrentOffset, buffer, totalRead, fileToRead)) {
                        return false;
                    }

                torrentOffset += fileToRead;
                totalRead += fileToRead;
                if (torrentOffset >= files[i].Length)
                {
                    torrentOffset = 0;
                    i++;
                }
            }
            return true;
        }
    }
}
