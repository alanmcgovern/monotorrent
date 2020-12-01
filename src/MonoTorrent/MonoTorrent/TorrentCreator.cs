//
// TorrentCreator.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006-2007 Gregor Burger and Alan McGovern
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
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;

using ReusableTasks;

namespace MonoTorrent
{
    public class TorrentCreator : EditableTorrent
    {
        internal class InputFile : ITorrentFileInfo
        {
            public string Path { get; set; }
            public string FullPath { get; set; }
            public byte[] MD5 { get; set; }
            public SemaphoreSlim Locker { get; } = new SemaphoreSlim (1, 1);
            public long Length { get; set; }

            internal InputFile (string path, long length)
                : this (path, path, length)
            {
            }

            internal InputFile (string sourcePath, string torrentPath, long length)
            {
                FullPath = sourcePath;
                Path = torrentPath;
                Length = length;
            }

            public BitField BitField => throw new NotImplementedException ();

            public Priority Priority {
                get => throw new NotImplementedException ();
                set => throw new NotImplementedException ();
            }

            public int StartPieceIndex => throw new NotImplementedException ();

            public int StartPieceOffset => throw new NotImplementedException ();

            public int EndPieceIndex => throw new NotImplementedException ();

            public (int startPiece, int endPiece) GetSelector ()
            {
                throw new NotImplementedException ();
            }
        }
        const int BlockSize = 16 * 1024;  // 16kB
        const int SmallestPieceSize = 2 * BlockSize;  // 32kB
        const int LargestPieceSize = 512 * BlockSize;  // 8MB

        public static int RecommendedPieceSize (long totalSize)
        {
            // Check all piece sizes that are multiples of 32kB and
            // choose the smallest piece size which results in a
            // .torrent file smaller than 60kb
            for (int i = SmallestPieceSize; i < LargestPieceSize; i *= 2) {
                int pieces = (int) (totalSize / i) + 1;
                if ((pieces * 20) < (60 * 1024))
                    return i;
            }

            // If we get here, we're hashing a massive file, so lets limit
            // to a reasonable maximum.
            return LargestPieceSize;
        }

        public static int RecommendedPieceSize (IEnumerable<string> files)
        {
            return RecommendedPieceSize (files.Sum (f => new FileInfo (f).Length));
        }

        public static int RecommendedPieceSize (IEnumerable<ITorrentFileInfo> files)
        {
            return RecommendedPieceSize (files.Sum (f => f.Length));
        }

        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
        {
            return RecommendedPieceSize (files.Sum (f => new FileInfo (f.Source).Length));
        }

        public event EventHandler<TorrentCreatorEventArgs> Hashed;

        public List<string> GetrightHttpSeeds { get; }
        public bool StoreMD5 { get; set; }

        internal TimeSpan ReadAllData_DequeueBufferTime;
        internal TimeSpan ReadAllData_EnqueueFilledBufferTime;
        internal TimeSpan ReadAllData_ReadTime;

        internal TimeSpan Hashing_DequeueFilledTime { get; set; }
        internal TimeSpan Hashing_HashingTime { get; set; }
        internal TimeSpan Hashing_EnqueueEmptyTime { get; set; }

        internal TimeSpan CreationTime { get; set; }

        public TorrentCreator ()
        {
            GetrightHttpSeeds = new List<string> ();
            CanEditSecureMetadata = true;
            CreatedBy = $"MonoTorrent {VersionInfo.Version}";
        }

        public BEncodedDictionary Create (ITorrentFileSource fileSource)
        {
            var timer = ValueStopwatch.StartNew ();
            BEncodedDictionary result = CreateAsync (fileSource, CancellationToken.None).GetAwaiter ().GetResult ();
            CreationTime = timer.Elapsed;
            return result;
        }

        public void Create (ITorrentFileSource fileSource, Stream stream)
        {
            CreateAsync (fileSource, stream, CancellationToken.None).GetAwaiter ().GetResult ();
        }

        public void Create (ITorrentFileSource fileSource, string savePath)
        {
            CreateAsync (fileSource, savePath, CancellationToken.None).GetAwaiter ().GetResult ();
        }

        public Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource)
        {
            return CreateAsync (fileSource, CancellationToken.None);
        }

        public Task CreateAsync (ITorrentFileSource fileSource, Stream stream)
        {
            return CreateAsync (fileSource, stream, CancellationToken.None);
        }

        public async Task CreateAsync (ITorrentFileSource fileSource, Stream stream, CancellationToken token)
        {
            Check.Stream (stream);

            BEncodedDictionary dictionary = await CreateAsync (fileSource, token);
            byte[] data = dictionary.Encode ();
            stream.Write (data, 0, data.Length);
        }

        public Task CreateAsync (ITorrentFileSource fileSource, string savePath)
        {
            return CreateAsync (fileSource, savePath, CancellationToken.None);
        }

        public async Task CreateAsync (ITorrentFileSource fileSource, string savePath, CancellationToken token)
        {
            Check.SavePath (savePath);

            BEncodedDictionary data = await CreateAsync (fileSource, token);
            File.WriteAllBytes (savePath, data.Encode ());
        }

        public async Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource, CancellationToken token)
        {
            Check.FileSource (fileSource);

            var mappings = new List<FileMapping> (fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException ("The file source must contain one or more files", nameof (fileSource));

            mappings.Sort ((left, right) => left.Destination.CompareTo (right.Destination));
            Validate (mappings);

            var maps = new List<InputFile> ();
            foreach (FileMapping m in fileSource.Files)
                maps.Add (new InputFile (m.Source, m.Destination, new FileInfo (m.Source).Length));
            return await CreateAsync (fileSource.TorrentName, maps, token);
        }

        internal async Task<BEncodedDictionary> CreateAsync (string name, List<InputFile> files)
        {
            return await CreateAsync (name, files, CancellationToken.None);
        }

        internal async Task<BEncodedDictionary> CreateAsync (string name, List<InputFile> files, CancellationToken token)
        {
            if (!InfoDict.ContainsKey (PieceLengthKey))
                PieceLength = RecommendedPieceSize (files);

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            var info = (BEncodedDictionary) torrent["info"];

            info["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            info["pieces"] = (BEncodedString) await CalcPiecesHash (files, token);

            if (files.Count == 1 && files[0].Path == name)
                CreateSingleFileTorrent (torrent, files);
            else
                CreateMultiFileTorrent (torrent, files);

            return torrent;
        }

        void AddCommonStuff (BEncodedDictionary torrent)
        {
            if (Announces.Count == 0) {
                torrent.Remove ("announce-list");
                if (Announce == null)
                    torrent.Remove ("announce");
                else
                    torrent["announce"] = new BEncodedString (Announce);
            } else {
                var tiers = new BEncodedList ();
                foreach (var initialTier in Announces) {
                    var tier = new BEncodedList ();
                    foreach (var v in initialTier)
                        tier.Add (new BEncodedString (v));
                    if (tier.Count > 0)
                        tiers.Add (tier);
                }
                if (tiers.Count > 0)
                    torrent["announce-list"] = tiers;
                else
                    torrent.Remove ("announce-list");
                torrent.Remove ("announce");
            }

            if (GetrightHttpSeeds.Count > 0) {
                var seedlist = new BEncodedList ();
                seedlist.AddRange (GetrightHttpSeeds.Select (s => (BEncodedString) s));
                torrent["url-list"] = seedlist;
            }

            TimeSpan span = DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            torrent["creation date"] = new BEncodedNumber ((long) span.TotalSeconds);
        }

        async Task<byte[]> CalcPiecesHash (List<InputFile> files, CancellationToken token)
        {
            long totalLength = files.Sum (t => t.Length);
            int pieceCount = (int) ((totalLength + PieceLength - 1) / PieceLength);

            // If the torrent will not give us at least 8 pieces per thread, try fewer threads. Then just run it
            // with parallel processing disabled if it's really tiny.
            int parallelFactor = Environment.ProcessorCount;
            while (pieceCount / parallelFactor < 8 && parallelFactor > 1)
                parallelFactor = Math.Max (parallelFactor / 2, 1);

            var tasks = new List<Task<byte[]>> ();
            int piecesPerPartition = pieceCount / parallelFactor;
            Queue<Synchronizer> synchronizers = Synchronizer.CreateLinked (parallelFactor);

            for (int i = 0; i < parallelFactor - 1; i++)
                tasks.Add (CalcPiecesHash (i * piecesPerPartition, piecesPerPartition * PieceLength, synchronizers.Dequeue (), files, token));
            tasks.Add (CalcPiecesHash (piecesPerPartition * (parallelFactor - 1), totalLength - ((parallelFactor - 1) * piecesPerPartition * PieceLength), synchronizers.Dequeue (), files, token));

            var hashes = new List<byte> ();
            foreach (Task<byte[]> task in tasks)
                hashes.AddRange (await task);
            return hashes.ToArray ();
        }

        async Task<byte[]> CalcPiecesHash (int startPiece, long totalBytesToRead, Synchronizer synchronizer, List<InputFile> files, CancellationToken token)
        {
            // One buffer will be filled and will be passed to the hashing method.
            // One buffer will be filled and will be waiting to be hashed.
            // One buffer will be empty and will be filled from the disk.
            // Aaaannd one extra buffer for good luck!
            var emptyBuffers = new AsyncProducerConsumerQueue<byte[]> (4);

            // Make this buffer one element larger so it can fit the placeholder which indicates a file has been completely read.
            var filledBuffers = new AsyncProducerConsumerQueue<(byte[], int, InputFile)> (emptyBuffers.Capacity + 1);

            // This is the IPieceWriter which we'll use to get our filestream. Each thread gets it's own writer.
            using IPieceWriter writer = CreateReader ();

            // Read from the disk in 256kB chunks, instead of 16kB, as a performance optimisation.
            // As the capacity is set to 4, this means we'll have 1 megabyte of buffers to handle.
            for (int i = 0; i < emptyBuffers.Capacity; i++)
                await emptyBuffers.EnqueueAsync (new byte[256 * 1024], token);
            token.ThrowIfCancellationRequested ();

            using CancellationTokenRegistration cancellation = token.Register (() => {
                emptyBuffers.CompleteAdding ();
                filledBuffers.CompleteAdding ();
            });

            // We're going to do single-threaded reading from disk, which (unfortunately) means we're (more or less) restricted
            // to single threaded hashing too as it's unlikely we'll have sufficient data in our buffers to do any better.
            Task readAllTask = ReadAllDataAsync (startPiece * PieceLength, totalBytesToRead, synchronizer, files, writer, emptyBuffers, filledBuffers, token);

            Task<byte[]> hashAllTask = HashAllDataAsync (totalBytesToRead, emptyBuffers, filledBuffers, token);

            Task firstCompleted = null;
            try {
                // We first call 'WhenAny' so that if an exception is thrown in one of the tasks, execution will continue
                // and we can kill the producer/consumer queues.
                firstCompleted = await Task.WhenAny (readAllTask, hashAllTask);

                // If the first completed task has faulted, force the exception to be thrown.
                await firstCompleted;
            } catch {
                // We got an exception from the first or second task, so bail out now!
                emptyBuffers.CompleteAdding ();
                filledBuffers.CompleteAdding ();
            }

            try {
                // If there is no exception from the first completed task, just wait for the second one.
                await Task.WhenAll (readAllTask, hashAllTask);
            } catch {
                token.ThrowIfCancellationRequested ();
                if (firstCompleted != null)
                    await firstCompleted;
                throw;
            }
            return await hashAllTask;
        }

        async Task ReadAllDataAsync (long startOffset, long totalBytesToRead, Synchronizer synchronizer, IList<InputFile> files, IPieceWriter writer, AsyncProducerConsumerQueue<byte[]> emptyBuffers, AsyncProducerConsumerQueue<(byte[], int, InputFile)> filledBuffers, CancellationToken token)
        {
            await MainLoop.SwitchToThreadpool ();

            await synchronizer.Self.Task;
            foreach (var file in files) {
                long fileRead = 0;
                if (startOffset >= file.Length) {
                    startOffset -= file.Length;
                    continue;
                }

                fileRead = startOffset;
                startOffset = 0;

                while (fileRead < file.Length && totalBytesToRead > 0) {
                    var timer = ValueStopwatch.StartNew ();
                    byte[] buffer = await emptyBuffers.DequeueAsync (token).ConfigureAwait (false);
                    ReadAllData_DequeueBufferTime += timer.Elapsed;

                    timer.Restart ();
                    int toRead = (int) Math.Min (buffer.Length, file.Length - fileRead);
                    toRead = (int) Math.Min (totalBytesToRead, toRead);

                    int read;
                    // FIXME: thread safety
                    read = await writer.ReadAsync (file, fileRead, buffer, 0, toRead);
                    if (read != toRead)
                        throw new InvalidOperationException ("The required data could not be read from the file.");
                    fileRead += read;
                    totalBytesToRead -= read;
                    ReadAllData_ReadTime += timer.Elapsed;

                    timer.Restart ();
                    await filledBuffers.EnqueueAsync ((buffer, read, file), token);
                    ReadAllData_EnqueueFilledBufferTime += timer.Elapsed;

                    if (emptyBuffers.Count == 0 && synchronizer.Next != synchronizer.Self) {
                        synchronizer.Next.SetResult (true);
                        await synchronizer.Self.Task;
                    }
                }
            }
            ReusableTaskCompletionSource<bool> next = synchronizer.Next;
            synchronizer.Disconnect ();
            next.SetResult (true);
            await filledBuffers.EnqueueAsync ((null, 0, null), token);
        }

        async Task<byte[]> HashAllDataAsync (long totalBytesToRead, AsyncProducerConsumerQueue<byte[]> emptyBuffers, AsyncProducerConsumerQueue<(byte[], int, InputFile)> filledBuffers, CancellationToken token)
        {
            await MainLoop.SwitchToThreadpool ();

            using MD5 md5Hasher = StoreMD5 ? HashAlgoFactory.MD5 () : null;
            using SHA1 shaHasher = HashAlgoFactory.SHA1 ();

            md5Hasher?.Initialize ();
            shaHasher?.Initialize ();

            // The current piece we're working on
            int piece = 0;
            // The number of bytes which have already been hashed for the current piece;
            int pieceHashedBytes = 0;
            // The buffer which will hold each piece hash. Each hash is 20 bytes.
            byte[] hashes = new byte[((totalBytesToRead + PieceLength - 1) / PieceLength) * 20];
            // The piece length
            int pieceLength = (int) PieceLength;

            long fileRead = 0;
            long totalRead = 0;
            while (true) {
                var timer = ValueStopwatch.StartNew ();
                (byte[] buffer, int count, InputFile file) = await filledBuffers.DequeueAsync (token);
                Hashing_DequeueFilledTime += timer.Elapsed;

                // If the buffer and file are both null then all files have been fully read.
                if (buffer == null && file == null) {
                    shaHasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                    Array.Copy (shaHasher.Hash, 0, hashes, piece * 20, shaHasher.Hash.Length);
                    break;
                }

                // If only the buffer is null then the current file has been fully read, but there are still more files to read.
                if (buffer == null) {
                    fileRead = 0;

                    if (md5Hasher != null) {
                        md5Hasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                        file.MD5 = md5Hasher.Hash;
                        md5Hasher.Initialize ();
                    }
                } else {
                    fileRead += count;
                    totalRead += count;

                    md5Hasher?.TransformBlock (buffer, 0, count, buffer, 0);
                    int bufferRead = 0;

                    timer.Restart ();
                    while (bufferRead < count) {
                        int bytesNeededForPiece = pieceLength - pieceHashedBytes;
                        int bytesToHash = Math.Min (bytesNeededForPiece, count - bufferRead);
                        shaHasher.TransformBlock (buffer, bufferRead, bytesToHash, buffer, bufferRead);

                        pieceHashedBytes += bytesToHash;
                        bufferRead += bytesToHash;

                        if (bytesNeededForPiece == 0) {
                            shaHasher.TransformFinalBlock (Array.Empty<byte> (), 0, 0);
                            Array.Copy (shaHasher.Hash, 0, hashes, piece * 20, shaHasher.Hash.Length);
                            shaHasher.Initialize ();
                            pieceHashedBytes = 0;
                            piece++;
                        }
                    }
                    Hashing_HashingTime += timer.Elapsed;

                    timer.Restart ();
                    await emptyBuffers.EnqueueAsync (buffer, token);
                    Hashing_EnqueueEmptyTime += timer.Elapsed;
                }
                Hashed?.InvokeAsync (this, new TorrentCreatorEventArgs (file.Path, fileRead, file.Length, totalRead, totalBytesToRead));
            }
            return hashes;
        }

        void CreateMultiFileTorrent (BEncodedDictionary dictionary, List<InputFile> mappings)
        {
            var info = (BEncodedDictionary) dictionary["info"];
            List<BEncodedValue> files = mappings.ConvertAll (ToFileInfoDict);
            info.Add ("files", new BEncodedList (files));
        }

        protected virtual IPieceWriter CreateReader ()
        {
            return new DiskWriter ();
        }

        void CreateSingleFileTorrent (BEncodedDictionary dictionary, IList<InputFile> mappings)
        {
            var infoDict = (BEncodedDictionary) dictionary["info"];
            infoDict.Add ("length", new BEncodedNumber (mappings[0].Length));
            if (mappings[0].MD5 != null)
                infoDict["md5sum"] = (BEncodedString) mappings[0].MD5;
        }

        static BEncodedValue ToFileInfoDict (InputFile file)
        {
            var fileDict = new BEncodedDictionary ();

            var filePath = new BEncodedList ();
            string[] splittetPath = file.Path.Split (new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in splittetPath)
                filePath.Add (new BEncodedString (s));

            fileDict["length"] = new BEncodedNumber (file.Length);
            fileDict["path"] = filePath;
            if (file.MD5 != null)
                fileDict["md5sum"] = (BEncodedString) file.MD5;

            return fileDict;
        }

        static void Validate (List<FileMapping> maps)
        {
            foreach (FileMapping map in maps)
                PathValidator.Validate (map.Destination);

            // Ensure all the destination files are unique too. The files should already be sorted.
            for (int i = 1; i < maps.Count; i++)
                if (maps[i - 1].Destination == maps[i].Destination)
                    throw new ArgumentException (
                        $"Files '{maps[i - 1].Source}' and '{maps[i].Source}' both map to the same destination '{maps[i].Destination}'");
        }
    }
}
