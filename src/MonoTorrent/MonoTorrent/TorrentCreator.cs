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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent
{
    public class  TorrentCreator : EditableTorrent
    {
        internal const int BlockSize = 16 * 1024;           // 16kB
        internal const int SmallestPiece = BlockSize * 2;   // 32kB
        internal const int LargestPiece = 8 * 1024 * 1024;  //  8MB

        public static int RecommendedPieceSize (long totalSize)
        {
            // Check all piece sizes that are multiples of 32kB and
            // choose the smallest piece size which results in a
            // .torrent file smaller than 60kb
            for (int i = SmallestPiece; i < LargestPiece; i *= 2) {
                int pieces = (int) (totalSize / i) + 1;
                if ((pieces * 20) < (60 * 1024))
                    return i;
            }

            // If we get here, we're hashing a massive file, so lets limit
            // to a max of 8MB pieces.
            return LargestPiece;
        }

        public static int RecommendedPieceSize (IEnumerable<string> files)
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f).Length));

        public static int RecommendedPieceSize (IEnumerable<TorrentFile> files)
            => RecommendedPieceSize (files.Sum (f => f.Length));

        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f.Source).Length));

        public event EventHandler<TorrentCreatorEventArgs> Hashed;

        public List<string> GetrightHttpSeeds { get; }

        public TimeSpan ReadAllData_DequeueBufferTime;
        public TimeSpan ReadAllData_EnqueueFilledBufferTime;
        public TimeSpan ReadAllData_ReadTime;

        public TimeSpan Hashing_DequeueFilledTime { get; set; }
        public TimeSpan Hashing_HashingTime { get; set; }
        public TimeSpan Hashing_EnqueueEmptyTime { get; set; }

        public TimeSpan CreationTime { get; set; }

        public bool StoreMD5 { get; set; }

        public int ParallelFactor = 1;

        public TorrentCreator ()
        {
            GetrightHttpSeeds = new List<string> ();
            CanEditSecureMetadata = true;
            CreatedBy = string.Format ("MonoTorrent {0}", VersionInfo.Version);
        }

        public BEncodedDictionary Create (ITorrentFileSource fileSource)
        {
            var timer = ValueStopwatch.StartNew ();
            var result = CreateAsync (fileSource, CancellationToken.None).GetAwaiter ().GetResult ();
            CreationTime = timer.Elapsed;
            return result;
        }

        public void Create(ITorrentFileSource fileSource, Stream stream)
            => CreateAsync (fileSource, stream, CancellationToken.None).GetAwaiter ().GetResult ();

        public void Create (ITorrentFileSource fileSource, string savePath)
            => CreateAsync (fileSource, savePath, CancellationToken.None).GetAwaiter ().GetResult ();

        internal BEncodedDictionary Create (string name, List<TorrentFile> files)
            => CreateAsync (name, files, CancellationToken.None).GetAwaiter ().GetResult ();

        public async Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource)
            => await CreateAsync (fileSource, CancellationToken.None);

        public async Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource, CancellationToken token)
        {
            Check.FileSource(fileSource);

            List <FileMapping> mappings = new List <FileMapping> (fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException ("The file source must contain one or more files", nameof (fileSource));

            mappings.Sort((left, right) => string.CompareOrdinal (left.Destination, right.Destination));
            Validate (mappings);

            List<TorrentFile> maps = new List <TorrentFile> ();
            foreach (FileMapping m in fileSource.Files)
                maps.Add (new TorrentFile (m.Destination, new FileInfo (m.Source).Length, m.Source));
            return await CreateAsync(fileSource.TorrentName, maps, token);
        }

        public Task CreateAsync(ITorrentFileSource fileSource, Stream stream)
            => CreateAsync (fileSource, stream, CancellationToken.None);

        public async Task CreateAsync(ITorrentFileSource fileSource, Stream stream, CancellationToken token)
        {
            Check.Stream(stream);

            var data = (await CreateAsync (fileSource, token)).Encode();
            stream.Write(data, 0, data.Length);
        }

        public Task CreateAsync (ITorrentFileSource fileSource, string savePath)
            => CreateAsync (fileSource, savePath, CancellationToken.None);

        public async Task CreateAsync(ITorrentFileSource fileSource, string savePath, CancellationToken token)
        {
            Check.SavePath(savePath);

            File.WriteAllBytes(savePath, (await CreateAsync(fileSource, token)).Encode());
        }

        internal async Task<BEncodedDictionary> CreateAsync(string name, List<TorrentFile> files, CancellationToken token)
        {
            if (!InfoDict.ContainsKey (PieceLengthKey))
                PieceLength = RecommendedPieceSize(files);

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            BEncodedDictionary info = (BEncodedDictionary) torrent ["info"];

            info ["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            using (IPieceWriter reader = CreateReader ()) {
                info ["pieces"] = (BEncodedString) await CalcPiecesHashAsync (files, reader, token);

                if (files.Count == 1 && files [0].Path == name)
                    CreateSingleFileTorrent (torrent, files, reader, name);
                else
                    CreateMultiFileTorrent (torrent, files, reader, name);
            }

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

        async Task<byte []> CalcPiecesHashAsync (List<TorrentFile> files, IPieceWriter writer, CancellationToken token)
        {
            var length = files.Sum (t => t.Length);
            var pieceCount = (int)(length / PieceLength);
            if (length % PieceLength != 0)
                pieceCount ++;

            // We need 20 bytes per hash.
            var torrentHashes = new byte[pieceCount * 20];

            var partition = pieceCount / ParallelFactor;
            var workers = new List<TorrentCreatorWorker> (ParallelFactor);
            var tasks = new List<Task> (ParallelFactor);

            for (int i = 0; i < ParallelFactor - 1; i ++)
                workers.Add (new TorrentCreatorWorker (i * partition, partition, PieceLength, files, writer));
            workers.Add (new TorrentCreatorWorker ((ParallelFactor - 1) * partition, pieceCount - (partition * (ParallelFactor - 1)), PieceLength, files, writer));

            foreach (var worker in workers)
                tasks.Add (worker.CalculateHashes (torrentHashes, token).AsTask ());

            await Task.WhenAll (tasks);

            Hashing_DequeueFilledTime = TimeSpan.FromTicks (workers.Sum (w => w.Hashing_DequeueFilledTime.Ticks));
            Hashing_HashingTime = TimeSpan.FromTicks (workers.Sum (w => w.Hashing_HashingTime.Ticks));
            Hashing_EnqueueEmptyTime = TimeSpan.FromTicks (workers.Sum (w => w.Hashing_EnqueueEmptyTime.Ticks));

            ReadAllData_DequeueBufferTime = TimeSpan.FromTicks (workers.Sum (w => w.ReadAllData_DequeueBufferTime.Ticks));
            ReadAllData_ReadTime = TimeSpan.FromTicks (workers.Sum (w => w.ReadAllData_ReadTime.Ticks));
            ReadAllData_EnqueueFilledBufferTime = TimeSpan.FromTicks (workers.Sum (w => w.ReadAllData_EnqueueFilledBufferTime.Ticks));

            return torrentHashes;
        }

        void CreateMultiFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, IPieceWriter writer, string name)
        {
            BEncodedDictionary info = (BEncodedDictionary) dictionary ["info"];
            List<BEncodedValue> files = mappings.ConvertAll<BEncodedValue> (ToFileInfoDict);
            info.Add ("files", new BEncodedList (files));
        }

        protected virtual IPieceWriter CreateReader ()
        {
            return new DiskWriter ();
        }

        void CreateSingleFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, IPieceWriter writer, string name)
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
