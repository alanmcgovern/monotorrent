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
        const int BlockSize =              16 * 1024;  // 16kB
        const int SmallestPieceSize =  2 * BlockSize;  // 32kB
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
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f).Length));

        public static int RecommendedPieceSize (IEnumerable<TorrentFile> files)
            => RecommendedPieceSize (files.Sum (f => f.Length));

        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f.Source).Length));

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
            CreatedBy = string.Format ("MonoTorrent {0}", VersionInfo.Version);
        }

        public BEncodedDictionary Create (ITorrentFileSource fileSource)
        {
            var timer = ValueStopwatch.StartNew ();
            var result = CreateAsync (fileSource, CancellationToken.None).GetAwaiter().GetResult();
            CreationTime = timer.Elapsed;
            return result;
        }

        public void Create(ITorrentFileSource fileSource, Stream stream)
            => CreateAsync (fileSource, stream, CancellationToken.None).GetAwaiter().GetResult();

        public void Create(ITorrentFileSource fileSource, string savePath)
            => CreateAsync (fileSource, savePath, CancellationToken.None).GetAwaiter ().GetResult ();

        public Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource)
            => CreateAsync (fileSource, CancellationToken.None);

        public Task CreateAsync (ITorrentFileSource fileSource, Stream stream)
            => CreateAsync (fileSource, stream, CancellationToken.None);

        public async Task CreateAsync(ITorrentFileSource fileSource, Stream stream, CancellationToken token)
        {
            Check.Stream(stream);

            var dictionary = await CreateAsync (fileSource, token);
            var data = dictionary.Encode();
            stream.Write(data, 0, data.Length);
        }

        public Task CreateAsync (ITorrentFileSource fileSource, string savePath)
            => CreateAsync (fileSource, savePath, CancellationToken.None);

        public async Task CreateAsync(ITorrentFileSource fileSource, string savePath, CancellationToken token)
        {
            Check.SavePath(savePath);

            var data = await CreateAsync (fileSource, token);
            File.WriteAllBytes(savePath, data.Encode());
        }

        public async Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource, CancellationToken token)
        {
            Check.FileSource(fileSource);

            List <FileMapping> mappings = new List <FileMapping> (fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException ("The file source must contain one or more files", "fileSource");

            mappings.Sort((left, right) => left.Destination.CompareTo(right.Destination));
            Validate (mappings);

            List<TorrentFile> maps = new List <TorrentFile> ();
            foreach (FileMapping m in fileSource.Files)
                maps.Add (new TorrentFile (m.Destination, new FileInfo (m.Source).Length, m.Source));
            return await CreateAsync(fileSource.TorrentName, maps, token);
        }

        internal async Task<BEncodedDictionary> CreateAsync(string name, List<TorrentFile> files)
            => await CreateAsync(name, files, CancellationToken.None);

        internal async Task<BEncodedDictionary> CreateAsync(string name, List<TorrentFile> files, CancellationToken token)
        {
            if (!InfoDict.ContainsKey (PieceLengthKey))
                PieceLength = RecommendedPieceSize(files);

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            BEncodedDictionary info = (BEncodedDictionary) torrent ["info"];

            info ["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            info ["pieces"] = (BEncodedString) await CalcPiecesHash (files, token);

            if (files.Count == 1 && files [0].Path == name)
                CreateSingleFileTorrent (torrent, files);
            else
                CreateMultiFileTorrent (torrent, files);

            return torrent;
        }

        void AddCommonStuff (BEncodedDictionary torrent)
        {
            if (Announces.Count == 0 || (Announces.Count == 1 && Announces [0].Count <= 1))
                RemoveCustom ("announce-list");

            if (Announces.Count > 0 && Announces [0].Count > 0)
                Announce = Announces [0] [0];

            if (GetrightHttpSeeds.Count > 0) {
                BEncodedList seedlist = new BEncodedList ();
                seedlist.AddRange (GetrightHttpSeeds.Select (s => (BEncodedString)s ));
                torrent ["url-list"] = seedlist;
            }

            TimeSpan span = DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            torrent ["creation date"] = new BEncodedNumber ((long) span.TotalSeconds);
        }

        async Task<byte []> CalcPiecesHash (List<TorrentFile> files, CancellationToken token)
        {
            long totalLength = files.Sum(t => t.Length);
            int pieceCount = (int)((totalLength + PieceLength - 1) / PieceLength);

            // If the torrent will not give us at least 8 pieces per thread, try fewer threads. Then just run it
            // with parallel processing disabled if it's really tiny.
            var parallelFactor = Environment.ProcessorCount;
            while (pieceCount / parallelFactor < 8 && parallelFactor > 1)
                parallelFactor = Math.Max (parallelFactor / 2, 1);

            var tasks = new List<Task<byte []>> ();
            var piecesPerPartition = pieceCount / parallelFactor;
            var synchronizers = Synchronizer.CreateLinked (parallelFactor);

            for (int i = 0; i < parallelFactor - 1; i++)
                tasks.Add (CalcPiecesHash (i * piecesPerPartition, piecesPerPartition * PieceLength, synchronizers.Dequeue (), files, token));
            tasks.Add (CalcPiecesHash (piecesPerPartition * (parallelFactor - 1), totalLength - ((parallelFactor - 1) * piecesPerPartition * PieceLength), synchronizers.Dequeue (), files, token));

            var hashes = new List<byte> ();
            foreach (var task in tasks)
                hashes.AddRange (await task);
            return hashes.ToArray ();
        }

        async Task<byte []> CalcPiecesHash (int startPiece, long totalBytesToRead, Synchronizer synchronizer, List<TorrentFile> files, CancellationToken token)
        {
            // One buffer will be filled and will be passed to the hashing method.
            // One buffer will be filled and will be waiting to be hashed.
            // One buffer will be empty and will be filled from the disk.
            // Aaaannd one extra buffer for good luck!
            var emptyBuffers = new AsyncProducerConsumerQueue<byte[]>(4);

            // Make this buffer one element larger so it can fit the placeholder which indicates a file has been completely read.
            var filledBuffers = new AsyncProducerConsumerQueue<(byte[], int, TorrentFile)>(emptyBuffers.Capacity + 1);

            // This is the IPieceWriter which we'll use to get our filestream. Each thread gets it's own writer.
            using var writer = CreateReader ();

            // Read from the disk in 256kB chunks, instead of 16kB, as a performance optimisation.
            // As the capacity is set to 4, this means we'll have 1 megabyte of buffers to handle.
            for (int i = 0; i < emptyBuffers.Capacity; i++)
                await emptyBuffers.EnqueueAsync(new byte[256 * 1024]);
            token.ThrowIfCancellationRequested();

            using var cancellation = token.Register(() => {
                emptyBuffers.CompleteAdding();
                filledBuffers.CompleteAdding();
            });

            // We're going to do single-threaded reading from disk, which (unfortunately) means we're (more or less) restricted
            // to single threaded hashing too as it's unlikely we'll have sufficient data in our buffers to do any better.
            var readAllTask = ReadAllDataAsync(startPiece * PieceLength, totalBytesToRead, synchronizer, files, writer, emptyBuffers, filledBuffers, token);

            var hashAllTask = HashAllDataAsync(startPiece, totalBytesToRead, emptyBuffers, filledBuffers, token);

            Task firstCompleted = null;
            try {
                // We first call 'WhenAny' so that if an exception is thrown in one of the tasks, execution will continue
                // and we can kill the producer/consumer queues.
                firstCompleted = await Task.WhenAny(readAllTask, hashAllTask);

                // If the first completed task has faulted, force the exception to be thrown.
                await firstCompleted;
            } catch {
                // We got an exception from the first or second task, so bail out now!
                emptyBuffers.CompleteAdding ();
                filledBuffers.CompleteAdding ();
            }

            try {
                // If there is no exception from the first completed task, just wait for the second one.
                await Task.WhenAll(readAllTask, hashAllTask);
            } catch {
                token.ThrowIfCancellationRequested();
                if (firstCompleted != null)
                    await firstCompleted;
                throw;
            }
            return await hashAllTask;
        }

        async Task ReadAllDataAsync (long startOffset, long totalBytesToRead, Synchronizer synchronizer, List<TorrentFile> files, IPieceWriter writer, AsyncProducerConsumerQueue<byte[]> emptyBuffers, AsyncProducerConsumerQueue<(byte[], int, TorrentFile)> filledBuffers, CancellationToken token)
        {
            long origStartOffset = startOffset;
            int read;
            await MainLoop.SwitchToThreadpool ();

            await synchronizer.Self.Task;

            foreach (TorrentFile file in files) {
                long fileRead = 0;
                if (startOffset >= file.Length) {
                    startOffset -= file.Length;
                    continue;
                }

                fileRead = startOffset;
                startOffset = 0;

                while (fileRead < file.Length && totalBytesToRead > 0) {
                    var timer = ValueStopwatch.StartNew ();
                    var buffer = await emptyBuffers.DequeueAsync (token).ConfigureAwait (false);
                    ReadAllData_DequeueBufferTime += timer.Elapsed;

                    timer.Restart ();
                    int toRead = (int)Math.Min(buffer.Length, file.Length - fileRead);
                    toRead = (int)Math.Min (totalBytesToRead, toRead);
                    
                    lock (writer)
                        read = writer.Read(file, fileRead, buffer, 0, toRead);
                    if (read != toRead)
                        throw new InvalidOperationException("The required data could not be read from the file.");
                    fileRead += read;
                    totalBytesToRead -= read;
                    ReadAllData_ReadTime += timer.Elapsed;

                    timer.Restart ();
                    await filledBuffers.EnqueueAsync((buffer, read, file), token);
                    ReadAllData_EnqueueFilledBufferTime += timer.Elapsed;

                    if (emptyBuffers.Count == 0 && synchronizer.Next != synchronizer.Self) {
                        synchronizer.Next.SetResult (true);
                        await synchronizer.Self.Task;
                    }
                }
            }
            var next = synchronizer.Next;
            synchronizer.Disconnect ();
            next.SetResult (true);
            await filledBuffers.EnqueueAsync((null, 0, null));
        }

        async Task<byte[]> HashAllDataAsync(int startPiece, long totalBytesToRead, AsyncProducerConsumerQueue<byte[]> emptyBuffers, AsyncProducerConsumerQueue<(byte[], int, TorrentFile)> filledBuffers, CancellationToken token)
        {
            await MainLoop.SwitchToThreadpool();

            using var md5Hasher = StoreMD5 ? HashAlgoFactory.Create<MD5> () : null;
            using var shaHasher = HashAlgoFactory.Create<SHA1> ();

            md5Hasher?.Initialize ();
            shaHasher?.Initialize ();

            // The current piece we're working on
            var piece = 0;
            // The number of bytes which have already been hashed for the current piece;
            int pieceHashedBytes = 0;
            // The buffer which will hold each piece hash. Each hash is 20 bytes.
            var hashes = new byte[((totalBytesToRead + PieceLength - 1) / PieceLength) * 20];
            // The piece length
            int pieceLength = (int) PieceLength;

            long fileRead = 0;
            long totalRead = 0;
            while (true) {
                var timer = ValueStopwatch.StartNew ();
                (var buffer, int count, var file) = await filledBuffers.DequeueAsync(token);
                Hashing_DequeueFilledTime += timer.Elapsed;

                // If the buffer and file are both null then all files have been fully read.
                if (buffer == null && file == null)
                {
                    shaHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    Array.Copy(shaHasher.Hash, 0, hashes, piece * 20, shaHasher.Hash.Length);
                    break;
                }

                // If only the buffer is null then the current file has been fully read, but there are still more files to read.
                if (buffer == null) {
                    fileRead = 0;

                    if (md5Hasher != null) {
                        md5Hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        file.MD5 = md5Hasher.Hash;
                        md5Hasher.Initialize();
                    }
                } else {
                    fileRead += count;
                    totalRead += count;

                    md5Hasher?.TransformBlock(buffer, 0, count, buffer, 0);
                    int bufferRead = 0;

                    timer.Restart ();
                    while (bufferRead < count) {
                        var bytesNeededForPiece = (int) (pieceLength - pieceHashedBytes);
                        var bytesToHash = Math.Min(bytesNeededForPiece, count - bufferRead);
                        shaHasher.TransformBlock(buffer, bufferRead, bytesToHash, buffer, bufferRead);

                        pieceHashedBytes += bytesToHash;
                        bufferRead += bytesToHash;

                        if (bytesNeededForPiece == 0)  {
                            shaHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                            Array.Copy(shaHasher.Hash, 0, hashes, piece * 20, shaHasher.Hash.Length);
                            shaHasher.Initialize ();
                            pieceHashedBytes = 0;
                            piece++;
                        }
                    }
                    Hashing_HashingTime += timer.Elapsed;

                    timer.Restart ();
                    await emptyBuffers.EnqueueAsync(buffer, token);
                    Hashing_EnqueueEmptyTime += timer.Elapsed;
                }
                Hashed?.InvokeAsync(this, new TorrentCreatorEventArgs(file.Path, fileRead, file.Length, totalRead, totalBytesToRead));
            }
            return hashes;
        }

        void CreateMultiFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings)
        {
            BEncodedDictionary info = (BEncodedDictionary) dictionary ["info"];
            List<BEncodedValue> files = mappings.ConvertAll (ToFileInfoDict);
            info.Add ("files", new BEncodedList (files));
        }

        protected virtual IPieceWriter CreateReader ()
        {
            return new DiskWriter ();
        }

        void CreateSingleFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings)
        {
            BEncodedDictionary infoDict = (BEncodedDictionary) dictionary ["info"];
            infoDict.Add ("length", new BEncodedNumber (mappings [0].Length));
            if (mappings [0].MD5 != null)
                infoDict ["md5sum"] = (BEncodedString) mappings [0].MD5;
        }

        static BEncodedValue ToFileInfoDict (TorrentFile file)
        {
            BEncodedDictionary fileDict = new BEncodedDictionary ();

            BEncodedList filePath = new BEncodedList ();
            string [] splittetPath = file.Path.Split (new char [] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in splittetPath)
                filePath.Add (new BEncodedString (s));

            fileDict ["length"] = new BEncodedNumber (file.Length);
            fileDict ["path"] = filePath;
            if (file.MD5 != null)
                fileDict ["md5sum"] = (BEncodedString) file.MD5;

            return fileDict;
        }

        static void Validate (List <FileMapping> maps)
        {
            // Make sure the user doesn't try to overwrite system files. Ensure
            // that the path is relative and doesn't try to access its parent folder
            var sepLinux = "/";
            var sepWindows = "\\";
            var dropLinux = "../";
            var dropWindows = "..\\";
            foreach (var map in maps) {
                if (map.Destination.StartsWith (sepLinux))
                    throw new ArgumentException ("The destination path cannot start with the '{0}' character", sepLinux);
                if (map.Destination.StartsWith (sepWindows))
                    throw new ArgumentException ("The destination path cannot start with the '{0}' character", sepWindows);

                if (map.Destination.Contains (dropLinux))
                    throw new ArgumentException ("The destination path cannot contain '{0}'", dropLinux);
                if (map.Destination.Contains (dropWindows))
                    throw new ArgumentException ("The destination path cannot contain '{0}'", dropWindows);
            }

            // Ensure all the destination files are unique too. The files should already be sorted.
            for (int i = 1; i < maps.Count; i++)
                if (maps[i - 1].Destination == maps [i].Destination)
                    throw new ArgumentException (string.Format ("Files '{0}' and '{1}' both map to the same destination '{2}'",
                                                 maps [i - 1].Source,
                                                 maps [i].Source,
                                                 maps [i].Destination));
        }
    }
}
