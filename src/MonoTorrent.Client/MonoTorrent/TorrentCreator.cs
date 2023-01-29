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
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.PieceWriter;

using ReusableTasks;

namespace MonoTorrent
{
    static class TorrentTypeExtensions
    {
        public static bool HasV1 (this TorrentType type)
            => type == TorrentType.V1Only || type == TorrentType.V1OnlyWithPaddingFiles || type == TorrentType.V1V2Hybrid;

        public static bool HasV2 (this TorrentType type)
            => type == TorrentType.V2Only || type == TorrentType.V1V2Hybrid;
    }

    public class TorrentCreator : EditableTorrent
    {
        class TorrentInfo : ITorrentInfo
        {
            public IList<ITorrentFile> Files { get; }
            public InfoHashes InfoHashes { get; }
            public string Name => "";
            public int PieceLength { get;}
            public long Size { get; }

            public TorrentInfo (InfoHashes infoHashes, IList<ITorrentFile> files, int pieceLength, long size)
            {
                InfoHashes = infoHashes;
                Files = files;
                PieceLength = pieceLength;
                Size = size;
            }
        }

        class TorrentManagerInfo : ITorrentManagerInfo
        {
            public IList<ITorrentManagerFile> Files { get; set; } = Array.Empty<ITorrentManagerFile> ();
            public InfoHashes InfoHashes => TorrentInfo.InfoHashes;
            public string Name => TorrentInfo.Name;
            public TorrentInfo TorrentInfo { get; set; }
            ITorrentInfo? ITorrentManagerInfo.TorrentInfo => TorrentInfo;

            public TorrentManagerInfo (IList<ITorrentManagerFile> files, TorrentInfo torrentInfo)
            {
                Files = files;
                TorrentInfo = torrentInfo;
            }
        }

        const int SmallestPieceSize = 2 * Constants.BlockSize;  // 32kB
        const int LargestPieceSize = 512 * Constants.BlockSize;  // 8MB

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

        public static int RecommendedPieceSize (IEnumerable<ITorrentManagerFile> files)
        {
            return RecommendedPieceSize (files.Sum (f => f.Length));
        }

        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
        {
            return RecommendedPieceSize (files.Sum (f => f.Length));
        }

        public event EventHandler<TorrentCreatorEventArgs>? Hashed;

        public List<string> GetrightHttpSeeds { get; }

        /// <summary>
        /// An MD5 checksum will be generated for each file when this is set to <see langword="true"/>.
        /// Defaults to false.
        /// </summary>
        public bool StoreMD5 { get; set; }

        bool StoreSHA1 => Type == TorrentType.V1OnlyWithPaddingFiles || Type == TorrentType.V1V2Hybrid;

        /// <summary>
        /// Determines whether 
        /// </summary>
        public TorrentType Type { get; }

        bool UsePadding => Type != TorrentType.V1Only;

        internal TimeSpan ReadAllData_DequeueBufferTime;
        internal TimeSpan ReadAllData_EnqueueFilledBufferTime;
        internal TimeSpan ReadAllData_ReadTime;

        internal TimeSpan Hashing_DequeueFilledTime { get; set; }
        internal TimeSpan Hashing_HashingTime { get; set; }
        internal TimeSpan Hashing_EnqueueEmptyTime { get; set; }

        internal TimeSpan CreationTime { get; set; }

        Factories Factories { get; }

        public TorrentCreator ()
            : this (TorrentType.V1V2Hybrid)
        {

        }
        public TorrentCreator (TorrentType type)
            : this (type, Factories.Default)
        {
            
        }
        public TorrentCreator (Factories factories)
            : this (TorrentType.V1V2Hybrid, factories)
        {

        }
        public TorrentCreator (TorrentType type, Factories factories)
        {
            GetrightHttpSeeds = new List<string> ();
            CanEditSecureMetadata = true;
            CreatedBy = $"MonoTorrent {GitInfoHelper.Version}";
            Factories = factories;
            Type = type;
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

            mappings.Sort ((left, right) => StringComparer.Ordinal.Compare (left.Destination, right.Destination));
            Validate (mappings);

            return await CreateAsync (fileSource.TorrentName, fileSource, token);
        }

        internal Task<BEncodedDictionary> CreateAsync (string name, ITorrentFileSource fileSource)
            => CreateAsync (name, fileSource, CancellationToken.None);

        internal async Task<BEncodedDictionary> CreateAsync (string name, ITorrentFileSource fileSource, CancellationToken token)
        {
            var source = fileSource.Files.ToArray ();
            if (source.All (t => t.Length == 0))
                throw new InvalidOperationException ("All files which were selected to be included this torrent have a length of zero. At least one file must have a non-zero length.");

            if (!InfoDict.ContainsKey (PieceLengthKey))
                PieceLength = RecommendedPieceSize (source.Sum (t => t.Length));

            var rawFiles = source.Select (file => {
                var length = file.Length;
                var padding = (int) ((UsePadding  && length % PieceLength > 0) ? PieceLength - (length % PieceLength) : 0);
                var info = (file.Destination, length, padding, file.Source);
                return info;
            }).ToArray ();

            // Hybrid and V2 torrents *must* hash files in the same order as they end up being stored in the bencoded dictionary,
            // which means they must be alphabetical.
            if (Type.HasV2 ())
                rawFiles = rawFiles.OrderBy (t => t.Destination, StringComparer.Ordinal).ToArray ();

            // The last non-empty file never has padding bytes
            var last = rawFiles.Where (t => t.length != 0).Last ();
            var index = Array.IndexOf (rawFiles, last);
            rawFiles[index].padding = 0;

            var files = TorrentFileInfo.Create (PieceLength, rawFiles);
            var manager = new TorrentManagerInfo (files,
                new TorrentInfo (
                    new InfoHashes (Type.HasV1 () ? InfoHash.FromMemory (new byte[20]) : null, Type.HasV2 () ? InfoHash.FromMemory (new byte[32]) : null),
                    files,
                    PieceLength,
                    files.Sum (t => t.Length + t.Padding)
                )
            );

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            var info = (BEncodedDictionary) torrent["info"];

            info["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            // V1 only torrents should not set this to increase backwards compatibility.
            // Torrents with V2 metadata (v2 only, or v1/v2 hybrid) should set it
            if (Type.HasV2 ())
                info["meta version"] = (BEncodedNumber) 2;

            (var sha1Hashes, var merkleLayers, var fileSHA1Hashes, var fileMD5Hashes) = await CalcPiecesHash (manager, token);
            if (!sha1Hashes.IsEmpty)
                info["pieces"] = BEncodedString.FromMemory (sha1Hashes);

            if (merkleLayers.Count > 0) {
                var dict = new BEncodedDictionary ();
                foreach (var kvp in merkleLayers.Where (t => t.Key.StartPieceIndex != t.Key.EndPieceIndex)) {
                    var rootHash = MerkleHash.Hash (kvp.Value.Span, PieceLength);
                    dict[BEncodedString.FromMemory (rootHash)] = BEncodedString.FromMemory (kvp.Value);
                }

                // If all files are smaller than PieceLength, then the piece layers dictionary will be empty.
                // That's ok, we must include the empty dictionary as it is a required key.
                torrent["piece layers"] = dict;

                var fileTree = new BEncodedDictionary ();
                foreach (var kvp in merkleLayers)
                    AppendFileTree (kvp.Key, kvp.Value, fileTree);
                info["file tree"] = fileTree;
            }

            if (Type.HasV1 ()) {
                if (manager.Files.Count == 1 && files[0].Path == name)
                    CreateSingleFileTorrent (torrent, merkleLayers, fileSHA1Hashes, fileMD5Hashes, files);
                else
                    CreateMultiFileTorrent (torrent, merkleLayers, fileSHA1Hashes, fileMD5Hashes, files);
            }

            return torrent;
        }

        void AppendFileTree (ITorrentManagerFile key, ReadOnlyMemory<byte> value, BEncodedDictionary fileTree)
        {
            var parts = key.Path.Split ('/');
            foreach (var part in parts) {
                if (!fileTree.TryGetValue (part, out BEncodedValue? inner)) {
                    fileTree[part] = inner = new BEncodedDictionary ();
                }
                fileTree = (BEncodedDictionary) inner;
            }
            if (value.Length > 32)
                value = MerkleHash.Hash (value.Span, PieceLength);

            var fileData = new BEncodedDictionary {
                {"length", (BEncodedNumber) key.Length },
                { "pieces root", BEncodedString.FromMemory (value) }
            };

            fileTree.Add ("", fileData);
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

        async Task<(
            ReadOnlyMemory<byte> sha1Hashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> merkleHashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes)>
        CalcPiecesHash (ITorrentManagerInfo manager, CancellationToken token)
        {
            var torrentInfo = manager.TorrentInfo ?? throw new InvalidOperationException ("manager.TorrentInfo should not be null");
            if (torrentInfo.Size == 0)
                return (default, new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> (), new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> (), new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> ());

            var settings = new EngineSettingsBuilder {
                DiskCacheBytes = PieceLength * 2, // Store the currently processing piece and allow preping the next piece
                DiskCachePolicy = StoreMD5 || StoreSHA1 ? CachePolicy.ReadsAndWrites : CachePolicy.WritesOnly
            }.ToSettings ();

            var pieceCount = torrentInfo.PieceCount ();

            using var diskManager = new DiskManager (settings, Factories);
            using var releaser = MemoryPool.Default.Rent (Constants.BlockSize, out Memory<Byte> reusableBlockBuffer);

            using var fileMD5 = StoreMD5 ? IncrementalHash.CreateHash (HashAlgorithmName.MD5) : null;
            using var fileSHA1 = StoreSHA1 ? IncrementalHash.CreateHash (HashAlgorithmName.SHA1) : null;

            var files = manager.Files.ToArray ().AsMemory ();
            while (files.Length > 0 && files.Span[0].Length == 0)
                files = files.Slice (1);

            // Store the MD5/SHA1 hash per file if needed.
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes = new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> ();
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes = new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> ();

            // All files will be SHA1 hashed into this array
            Memory<byte> sha1Hashes = Type.HasV1 () ? new byte[pieceCount * 20] : Array.Empty<byte> ();

            // All files will be merkle hashed into individual merkle trees
            Memory<byte> merkleHashes = Type.HasV2 () ? new byte[pieceCount * 32] : Array.Empty<byte> ();

            var hashes = new PieceHash (sha1Hashes.IsEmpty ? sha1Hashes : sha1Hashes.Slice (0, 20), merkleHashes.IsEmpty ? merkleHashes : merkleHashes.Slice (0, 32));
            var currentPiece = diskManager.GetHashAsync (manager, 0, hashes).ConfigureAwait (false);
            var previousAppend = ReusableTask.CompletedTask;
            for (int piece = 0; piece < pieceCount; piece++) {
                token.ThrowIfCancellationRequested ();
                // Wait for the current piece's async read to complete. When this task completes, the V1 and/or V2 hash will
                // be stored in the 'hashes' object
                await currentPiece;

                var currentFile = files.Span[0];
                var sizeOfCurrentPiece = torrentInfo.BytesPerPiece (piece);

                if (currentFile.EndPieceIndex == piece) {
                    while (currentFile != null && currentFile.EndPieceIndex == piece) {
                        OnHashed (new TorrentCreatorEventArgs (currentFile.FullPath, currentFile.Length, currentFile.Length, torrentInfo.PieceIndexToByteOffset(piece) + sizeOfCurrentPiece, torrentInfo.Size));

                        files = files.Slice (1);
                        currentFile = files.Length == 0 ? null : files.Span[0];
                    }
                } else {
                    OnHashed (new TorrentCreatorEventArgs (currentFile.FullPath, torrentInfo.PieceIndexToByteOffset (piece) - currentFile.OffsetInTorrent + sizeOfCurrentPiece, currentFile.Length, torrentInfo.PieceIndexToByteOffset (piece) + sizeOfCurrentPiece, torrentInfo.Size));
                }

                // Asynchronously begin reading the *next* piece and computing the hash for that piece.
                var nextPiece = piece + 1;
                if (nextPiece < pieceCount) {
                    hashes = new PieceHash (sha1Hashes.IsEmpty ? sha1Hashes : sha1Hashes.Slice (nextPiece * 20, 20), merkleHashes.IsEmpty ? merkleHashes : merkleHashes.Slice (nextPiece * 32, 32));
                    currentPiece = diskManager.GetHashAsync (manager, nextPiece, hashes).ConfigureAwait (false);
                }

                // While we're computing the hash for 'piece + 1', we can compute the MD5 and/or SHA1 for the specific file
                // being hashed.
                if (StoreMD5 || StoreSHA1) {
                    for (int i = 0; i < torrentInfo.BlocksPerPiece (piece); i++) {
                        var buffer = reusableBlockBuffer.Slice (0, torrentInfo.BytesPerBlock (piece, i));
                        await diskManager.ReadAsync (manager, new BlockInfo (piece, i * Constants.BlockSize, buffer.Length), reusableBlockBuffer).ConfigureAwait (false);
                        await AppendPerFileHashes (manager, fileMD5, fileMD5Hashes, fileSHA1, fileSHA1Hashes, (long) torrentInfo.PieceLength * piece + i * Constants.BlockSize, buffer).ConfigureAwait (false);
                    }
                }
            }
            // Ensure the final block from the final piece is hashed.
            await previousAppend;

            var merkleLayers = new Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> ();
            if (merkleHashes.Length > 0) {
                foreach (var file in manager.Files)
                    merkleLayers.Add (file, merkleHashes.Slice (file.StartPieceIndex * 32, (file.EndPieceIndex - file.StartPieceIndex + 1) * 32));
            }
            return (sha1Hashes, merkleLayers, fileSHA1Hashes, fileMD5Hashes);
        }

        protected virtual void OnHashed (TorrentCreatorEventArgs args)
            => Hashed?.InvokeAsync (this, args);

        async ReusableTask AppendPerFileHashes (ITorrentManagerInfo manager, IncrementalHash? fileMD5, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes, IncrementalHash? fileSHA1, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes, long offset, Memory<byte> buffer)
        {
            while (buffer.Length > 0) {
                var fileIndex = manager.Files.FindFileByOffset (offset);
                var file = manager.Files[fileIndex];
                var remainingBytes = (file.OffsetInTorrent + file.Length) - offset;

                // If this is purely padding bytes, skip them.
                if (remainingBytes <= 0)
                    break;

                if (remainingBytes <= buffer.Length) {
                    fileMD5?.AppendData (buffer.Span.Slice (0, (int) remainingBytes));
                    fileSHA1?.AppendData (buffer.Span.Slice (0, (int) remainingBytes));

                    buffer = buffer.Slice ((int) remainingBytes);
                    offset += remainingBytes;

                    if (!(fileSHA1 is null))
                        fileSHA1Hashes.Add (file, fileSHA1.GetHashAndReset ());
                    if (!(fileMD5 is null))
                        fileMD5Hashes.Add (file, fileMD5.GetHashAndReset ());
                } else {
                    static async ReusableTask AsyncHash (IncrementalHash hash, Memory<byte> buffer)
                    {
                        await MainLoop.SwitchThread ();
                        hash.AppendData (buffer.Span);
                    }
                    if (!(fileMD5 is null) && !(fileSHA1 is null)) {
                        // Both of these are non-null, so hash both in parallel
                        var t1 = fileMD5 is null ? ReusableTask.CompletedTask : AsyncHash (fileMD5, buffer);
                        var t2 = fileSHA1 is null ? ReusableTask.CompletedTask : AsyncHash (fileSHA1, buffer);
                        await t1.ConfigureAwait (false);
                        await t2.ConfigureAwait (false);
                    } else {
                        // Only one of them is non-null, so no fancy threading needed.
                        (fileMD5 ?? fileSHA1)!.AppendData (buffer.Span);
                    }
                    break;
                }
            }
        }

        void CreateMultiFileTorrent (
            BEncodedDictionary dictionary,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> merkleHashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes,
            IList<ITorrentManagerFile> mappings)
        {
            var info = (BEncodedDictionary) dictionary["info"];
            List<BEncodedValue> files = mappings
                .Select (t => ToFileInfoDicts (t, fileMD5Hashes, fileSHA1Hashes))
                .SelectMany (x => x)
                .ToList ();
            info.Add ("files", new BEncodedList (files));
        }

        void CreateSingleFileTorrent (
            BEncodedDictionary dictionary,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> merkleHashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes,
            Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes,
            IList<ITorrentManagerFile> mappings)
        {
            var infoDict = (BEncodedDictionary) dictionary["info"];
            infoDict.Add ("length", new BEncodedNumber (mappings[0].Length));
            if (fileMD5Hashes?.ContainsKey (mappings[0]) ?? false)
                infoDict["md5sum"] = BEncodedString.FromMemory (fileMD5Hashes[mappings[0]]);
            if (fileSHA1Hashes?.ContainsKey (mappings[0]) ?? false)
                infoDict["sha1"] = BEncodedString.FromMemory (fileSHA1Hashes[mappings[0]]);
        }

        // converts InputFile into one BEncodedDictionary when there's no padding, or two BEncodedDictionaries when there is.
        static BEncodedValue[] ToFileInfoDicts (ITorrentManagerFile file, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes)
        {
            return (file.Padding > 0) ?
                new[] { ToFileInfoDict (file, fileMD5Hashes, fileSHA1Hashes), ToPaddingFileInfoDict (file, fileMD5Hashes) } : new[] { ToFileInfoDict (file, fileMD5Hashes, fileSHA1Hashes) };
        }

        static BEncodedValue ToFileInfoDict (ITorrentManagerFile file, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileSHA1Hashes)
        {
            var fileDict = new BEncodedDictionary ();

            var filePath = new BEncodedList ();
            string[] splittetPath = file.Path.Split (new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in splittetPath)
                filePath.Add (new BEncodedString (s));

            fileDict["length"] = new BEncodedNumber (file.Length);
            fileDict["path"] = filePath;
            if (fileMD5Hashes.ContainsKey (file))
                fileDict["md5sum"] = BEncodedString.FromMemory (fileMD5Hashes[file]);
            if (fileSHA1Hashes.ContainsKey (file))
                fileDict["sha1"] = BEncodedString.FromMemory (fileSHA1Hashes[file]);


            return fileDict;
        }

        static BEncodedValue ToPaddingFileInfoDict (ITorrentManagerFile file, Dictionary<ITorrentManagerFile, ReadOnlyMemory<byte>> fileMD5Hashes)
        {
            var fileDict = new BEncodedDictionary ();

            var filePath = new BEncodedList ();
            filePath.Add (new BEncodedString (".pad"));
            filePath.Add (new BEncodedString ($"{file.Padding}"));

            fileDict["length"] = new BEncodedNumber (file.Padding);
            fileDict["path"] = filePath;

            if (fileMD5Hashes.ContainsKey (file)) {
                using MD5 md5Hasher = MD5.Create ();
                fileDict["md5sum"] = (BEncodedString) md5Hasher.ComputeHash (new byte[file.Padding]);
            }

            fileDict["attr"] = (BEncodedString) "p";
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
