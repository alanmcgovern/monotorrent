//
// Torrent.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public sealed class Torrent : ITorrentInfo, IEquatable<Torrent>
    {
        static Dictionary<int, ReadOnlyMemory<byte>> FinalLayerHash { get; } = CreateFinalHashPerLayer ();

        static Dictionary<int, ReadOnlyMemory<byte>>  CreateFinalHashPerLayer ()
        {
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
            byte[] buffer = new byte[32];

            Dictionary<int, ReadOnlyMemory<byte>> results = new Dictionary<int, ReadOnlyMemory<byte>> ();
            results[Constants.BlockSize] = (byte[]) buffer.Clone ();
            for (int i = Constants.BlockSize * 2; i <= Constants.MaximumPieceLength; i *= 2) {
                hasher.AppendData (buffer);
                hasher.AppendData (buffer);
                if (!hasher.TryGetHashAndReset (buffer, out int written) || written != 32)
                    throw new Exception ("Critical failure");
                results[i] = (byte[]) buffer.Clone ();
            }
            return results;
        }

        internal static bool SupportsV2Torrents = false;

        /// <summary>
        /// This method loads a .torrent file from the specified path.
        /// </summary>
        /// <param name="path">The path to load the .torrent file from</param>
        public static Torrent Load (string path)
        {
            if (string.IsNullOrEmpty (path))
                throw new ArgumentException ("value must not be null or empty", nameof (path));

            using Stream s = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Load (s, default);
        }

        /// <summary>
        /// Loads a torrent from the provided <see cref="BEncodedDictionary"/>
        /// </summary>
        /// <param name="dictionary">The BEncodedDictionary containing the torrent data</param>
        /// <returns></returns>
        public static Torrent Load (BEncodedDictionary dictionary)
        {
            using (MemoryPool.Default.Rent (dictionary.LengthInBytes (), out Memory<byte> buffer)) {
                dictionary.Encode (buffer.Span);
                return Load (buffer.Span);
            }
        }

        /// <summary>
        /// Loads a torrent from the provided Span
        /// </summary>
        /// <param name="span">The Span containing the data</param>
        /// <returns></returns>
        public static Torrent Load (ReadOnlySpan<byte> span)
            => Load (null, span);

        /// <summary>
        /// Loads a .torrent from the supplied stream
        /// </summary>
        /// <param name="stream">The stream containing the data to load</param>
        /// <returns></returns>
        public static Torrent Load (Stream stream)
            => Load (stream ?? throw new ArgumentNullException (nameof (stream)), default);

        /// <summary>
        /// Called from either Load(stream) or Load(string).
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        static Torrent Load (Stream stream, ReadOnlySpan<byte> buffer)
        {
            try {
                (var torrentDict, var infoHashes) = stream != null ? BEncodedDictionary.DecodeTorrent (stream) : BEncodedDictionary.DecodeTorrent (buffer);
                var t = new Torrent ();
                t.LoadInternal (torrentDict, infoHashes);
                return t;
            } catch (BEncodingException ex) {
                throw new TorrentException ("Invalid torrent file specified", ex);
            }
        }

        /// <summary>
        /// This method loads a .torrent file from the specified path.
        /// </summary>
        /// <param name="path">The path to load the .torrent file from</param>
        public static Task<Torrent> LoadAsync (string path)
            => Task.Run (() => Load (path));

        /// <summary>
        /// Loads a torrent from a Memory containing the bencoded data
        /// </summary>
        /// <param name="memory">The Memory containing the data</param>
        /// <returns></returns>
        public static Task<Torrent> LoadAsync (Memory<byte> memory)
            => Task.Run (() => Load (memory.Span));

        /// <summary>
        /// Loads a .torrent from the supplied stream
        /// </summary>
        /// <param name="stream">The stream containing the data to load</param>
        /// <returns></returns>
        public static Task<Torrent> LoadAsync (Stream stream)
            => Task.Run (() => Load (stream));

        /// <summary>
        /// Loads a .torrent file from the specified URL
        /// </summary>
        /// <param name="client">The HttpClient used to download the url</param>
        /// <param name="url">The URL to download the .torrent from</param>
        /// <param name="savePath">The path to download the .torrent to before it gets loaded</param>
        /// <returns></returns>
        public static async Task<Torrent> LoadAsync (HttpClient client, Uri url, string savePath)
        {
            try {
                File.WriteAllBytes (savePath, await client.GetByteArrayAsync (url));
            } catch (Exception ex) {
                File.Delete (savePath);
                throw new TorrentException ("Could not download .torrent file from the specified url", ex);
            }

            return await LoadAsync (savePath).ConfigureAwait (false);
        }

        /// <summary>
        /// Loads a .torrent from the specificed path. A return value indicates
        /// whether the operation was successful.
        /// </summary>
        /// <param name="path">The path to load the .torrent file from</param>
        /// <param name="torrent">If the loading was succesful it is assigned the Torrent</param>
        /// <returns>True if successful</returns>
        public static bool TryLoad (string path, out Torrent torrent)
        {
            if (string.IsNullOrEmpty (path))
                throw new ArgumentNullException ("Value must not be null or empty", nameof (path));

            torrent = null;
            try {
                if (File.Exists (path))
                    torrent = Load (path);
            } catch {
                // We will return false if an exception is thrown as 'torrent' will still
                // be null.
            }

            return torrent != null;
        }

        /// <summary>
        /// Loads a .torrent from the specified Span. A return value indicates
        /// whether the operation was successful.
        /// </summary>
        /// <param name="span">The Span to load the .torrent from</param>
        /// <param name="torrent">If loading was successful, it contains the Torrent</param>
        /// <returns>True if successful</returns>
        public static bool TryLoad (ReadOnlySpan<byte> span, out Torrent torrent)
        {
            try {
                torrent = Load (span);
            } catch {
                torrent = null;
            }

            return torrent != null;
        }

        /// <summary>
        /// Loads a .torrent from the supplied stream. A return value indicates
        /// whether the operation was successful.
        /// </summary>
        /// <param name="stream">The stream containing the data to load</param>
        /// <param name="torrent">If the loading was succesful it is assigned the Torrent</param>
        /// <returns>True if successful</returns>
        public static bool TryLoad (Stream stream, out Torrent torrent)
        {
            if (stream is null)
                throw new ArgumentNullException (nameof (stream));

            try {
                torrent = Load (stream);
            } catch {
                torrent = null;
            }

            return torrent != null;
        }

        static DateTime UnixEpoch => new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The announce URLs contained within the .torrent file
        /// </summary>
        public IList<IList<string>> AnnounceUrls { get; private set; }

        /// <summary>
        /// The comment contained within the .torrent file
        /// </summary>
        public string Comment { get; private set; }

        /// <summary>
        /// The optional string showing who/what created the .torrent
        /// </summary>
        public string CreatedBy { get; private set; }

        /// <summary>
        /// The creation date of the .torrent file
        /// </summary>
        public DateTime CreationDate { get; private set; }

        /// <summary>
        /// The optional ED2K hash contained within the .torrent file
        /// </summary>
        public ReadOnlyMemory<byte> ED2K { get; private set; }

        /// <summary>
        /// The encoding used by the client that created the .torrent file
        /// </summary>
        public string Encoding { get; private set; }

        /// <summary>
        /// The list of files contained within the .torrent which are available for download
        /// </summary>
        public IList<ITorrentFile> Files { get; private set; }

        /// <summary>
        /// This is the http-based seeding (getright protocole)
        /// </summary>
        public IList<Uri> HttpSeeds { get; }

        /// <summary>
        /// This is the infohash generated by putting the "Info" section of a .torrent
        /// through a SHA1 hasher.
        /// </summary>
        public InfoHash InfoHash { get; private set; }

        /// <summary>
        /// This is the infohash generated by putting the "Info" section of a .torrent
        /// through a SHA256 hasher.
        /// </summary>
        public InfoHash InfoHashV2 { get; private set; }

        /// <summary>
        /// The 'info' dictionary encoded as a byte array.
        /// </summary>
        internal byte[] InfoMetadata { get; private set; }

        /// <summary>
        /// Shows whether DHT is allowed or not. If it is a private torrent, no peer
        /// sharing should be allowed.
        /// </summary>
        public bool IsPrivate { get; private set; }

        /// <summary>
        /// In the case of a single file torrent, this is the name of the file.
        /// In the case of a multi file torrent, it is the name of the root folder.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The list of DHT nodes which can be used to bootstrap this torrent.
        /// </summary>
        public BEncodedList Nodes { get; private set; }

        /// <summary>
        /// The length of each piece in bytes.
        /// </summary>
        public int PieceLength { get; private set; }

        /// <summary>
        /// The number of pieces in the torrent.
        /// </summary>
        public int PieceCount => PieceHashes != null ? PieceHashes.Count : PieceHashesV2.Count;

        /// <summary>
        /// This is the array of SHA1 piece hashes contained within the torrent. Used to validate torrents which comply with the V1 specification.
        /// </summary>
        public IPieceHashes PieceHashes { get; private set; }

        /// <summary>
        /// This is the array of SHA256 piece hashes contained within the torrent. Used to validate torrents which comply with the V2 specification.
        /// </summary>
        public IPieceHashes PieceHashesV2 { get; private set; }

        /// <summary>
        /// The name of the Publisher
        /// </summary>
        public string Publisher { get; private set; }

        /// <summary>
        /// The Url of the publisher of either the content or the .torrent file
        /// </summary>
        public string PublisherUrl { get; private set; }

        /// <summary>
        /// The optional SHA1 hash contained within the .torrent file
        /// </summary>
        public ReadOnlyMemory<byte> SHA1 { get; private set; }

        /// <summary>
        /// The source of the torrent
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// The size of all files in bytes.
        /// </summary>
        public long Size { get; private set; }

        Torrent ()
        {
            Comment = string.Empty;
            CreatedBy = string.Empty;
            CreationDate = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Encoding = string.Empty;
            Name = string.Empty;
            Publisher = string.Empty;
            PublisherUrl = string.Empty;
            HttpSeeds = new List<Uri> ();
        }

        public override bool Equals (object obj)
            => Equals (obj as Torrent);

        public bool Equals (Torrent other)
            => InfoHash == other?.InfoHash;

        public override int GetHashCode ()
            => InfoHash.GetHashCode ();

        public override string ToString ()
            => Name;

        /// <summary>
        /// This method is called internally to load the information found within the "Info" section
        /// of the .torrent file
        /// </summary>
        /// <param name="dictionary">The dictionary representing the Info section of the .torrent file</param>
        void ProcessInfo (BEncodedDictionary dictionary)
        {
            InfoMetadata = dictionary.Encode ();
            PieceLength = int.Parse (dictionary["piece length"].ToString ());
            bool hasV1Data = false;
            bool hasV2Data = false;

            if (dictionary.TryGetValue ("meta version", out BEncodedValue metaVersion)) {
                if (metaVersion is BEncodedNumber metadataVersion) {
                    hasV2Data = metadataVersion.Number == 2;
                }
            } else {
                hasV1Data = true;
            }

            hasV1Data = dictionary.ContainsKey ("pieces");
            hasV2Data |= dictionary.ContainsKey ("file tree");

            if (!hasV1Data) {
                if (hasV2Data) {
                    if (!SupportsV2Torrents) {
                        throw new TorrentException ("This torrent contains metadata for bittorrent v2 only. MonoTorrent only supports v1 torrents, or hybrid torrents with v1 and v2 metadata.");
                    }
                } else {
                    throw new TorrentException ("Unsupported torrent version detected.");
                }
            }

            if (hasV1Data) {
                var data = ((BEncodedString) dictionary["pieces"]).AsMemory ();
                if (data.Length % 20 != 0)
                    throw new TorrentException ("Invalid infohash detected");
                PieceHashes = new PieceHashesV1 (data, 20);
            }

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                switch (keypair.Key.Text) {
                    case ("source"):
                        Source = keypair.Value.ToString ();
                        break;

                    case ("sha1"):
                        SHA1 = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("ed2k"):
                        ED2K = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("publisher-url.utf-8"):
                        if (keypair.Value.ToString ().Length > 0)
                            PublisherUrl = keypair.Value.ToString ();
                        break;

                    case ("publisher-url"):
                        if ((string.IsNullOrEmpty (PublisherUrl)) && (keypair.Value.ToString ().Length > 0))
                            PublisherUrl = keypair.Value.ToString ();
                        break;

                    case ("publisher.utf-8"):
                        if (keypair.Value.ToString ().Length > 0)
                            Publisher = keypair.Value.ToString ();
                        break;

                    case ("publisher"):
                        if ((string.IsNullOrEmpty (Publisher)) && (keypair.Value.ToString ().Length > 0))
                            Publisher = keypair.Value.ToString ();
                        break;

                    case ("files"):
                        // This is the list of files using the v1 torrent format.
                        // Only load if we have not processed filesv2
                        if (Files == null)
                            Files = LoadTorrentFilesV1 ((BEncodedList) keypair.Value, PieceLength);
                        break;

                    case "file tree":
                        // This is the list of files using the v2 torrent format.
                        Files = LoadTorrentFilesV2 ((BEncodedDictionary) dictionary["file tree"], PieceLength);
                        break;
                    case ("name.utf-8"):
                        if (keypair.Value.ToString ().Length > 0)
                            Name = keypair.Value.ToString ();
                        break;

                    case ("name"):
                        if ((string.IsNullOrEmpty (Name)) && (keypair.Value.ToString ().Length > 0))
                            Name = keypair.Value.ToString ();
                        break;

                    case ("piece length"):  // Already handled
                        break;

                    case ("length"):
                        break;      // This is a singlefile torrent

                    case ("private"):
                        IsPrivate = (keypair.Value.ToString () == "1") ? true : false;
                        break;

                    default:
                        break;
                }
            }

            if (Files != null)
                Size = Files.Select (f => f.Length).Sum ();

            if (Files == null && hasV1Data)   // Not a multi-file torrent
            {
                long length = long.Parse (dictionary["length"].ToString ());
                Size = length;
                string path = Name;
                int endPiece = Math.Min (PieceCount - 1, (int) ((Size + (PieceLength - 1)) / PieceLength));
                Files = Array.AsReadOnly<ITorrentFile> (new[] { new TorrentFile (path, length, 0, endPiece, 0) });
            }
        }

        void LoadInternal (BEncodedDictionary torrentInformation, InfoHashes infoHashes)
        {
            Check.TorrentInformation (torrentInformation);
            AnnounceUrls = new List<IList<string>> ().AsReadOnly ();

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in torrentInformation) {
                switch (keypair.Key.Text) {
                    case ("announce"):
                        // Ignore this if we have an announce-list
                        if (torrentInformation.ContainsKey ("announce-list"))
                            break;
                        AnnounceUrls = new List<IList<string>> {
                            new List<string> { keypair.Value.ToString () }.AsReadOnly ()
                        }.AsReadOnly ();
                        break;

                    case ("creation date"):
                        try {
                            try {
                                CreationDate = UnixEpoch.AddSeconds (long.Parse (keypair.Value.ToString ()));
                            } catch (Exception e) {
                                if (e is ArgumentOutOfRangeException)
                                    CreationDate = UnixEpoch.AddMilliseconds (long.Parse (keypair.Value.ToString ()));
                                else
                                    throw;
                            }
                        } catch (Exception e) {
                            if (e is ArgumentOutOfRangeException)
                                throw new BEncodingException ("Argument out of range exception when adding seconds to creation date.", e);
                            else if (e is FormatException)
                                throw new BEncodingException ($"Could not parse {keypair.Value} into a number", e);
                            else
                                throw;
                        }
                        break;

                    case ("nodes"):
                        if (keypair.Value is BEncodedList list)
                            Nodes = list;
                        break;

                    case ("comment.utf-8"):
                        if (keypair.Value.ToString ().Length != 0)
                            Comment = keypair.Value.ToString ();       // Always take the UTF-8 version
                        break;                                          // even if there's an existing value

                    case ("comment"):
                        if (string.IsNullOrEmpty (Comment))
                            Comment = keypair.Value.ToString ();
                        break;

                    case ("publisher-url.utf-8"):                       // Always take the UTF-8 version
                        PublisherUrl = keypair.Value.ToString ();      // even if there's an existing value
                        break;

                    case ("publisher-url"):
                        if (string.IsNullOrEmpty (PublisherUrl))
                            PublisherUrl = keypair.Value.ToString ();
                        break;

                    case ("created by"):
                        CreatedBy = keypair.Value.ToString ();
                        break;

                    case ("encoding"):
                        Encoding = keypair.Value.ToString ();
                        break;

                    case ("info"):
                        ProcessInfo (((BEncodedDictionary) keypair.Value));
                        if (PieceHashes != null)
                            InfoHash = InfoHash.FromMemory (infoHashes.SHA1);
                        if (SupportsV2Torrents && !infoHashes.SHA256.IsEmpty)
                            InfoHashV2 = InfoHash.FromMemory (infoHashes.SHA256);
                        break;

                    case ("name"):                                               // Handled elsewhere
                        break;

                    case ("announce-list"):
                        if (keypair.Value is BEncodedString)
                            break;

                        var result = new List<IList<string>> ();
                        var announces = (BEncodedList) keypair.Value;
                        for (int j = 0; j < announces.Count; j++) {
                            if (announces[j] is BEncodedList bencodedTier) {
                                var tier = new List<string> (bencodedTier.Count);

                                for (int k = 0; k < bencodedTier.Count; k++)
                                    tier.Add (bencodedTier[k].ToString ());

                                var resultTier = new List<string> ();
                                for (int k = 0; k < tier.Count; k++)
                                    resultTier.Add (tier[k]);

                                if (resultTier.Count != 0)
                                    result.Add (tier.AsReadOnly ());
                            } else {
                                throw new BEncodingException (
                                    $"Non-BEncodedList found in announce-list (found {announces[j].GetType ()})");
                            }
                        }
                        if (result.Count > 0)
                            AnnounceUrls = result.AsReadOnly ();
                        break;

                    case ("httpseeds"):
                        // This form of web-seeding is not supported.
                        break;

                    case "piece layers":
                        PieceHashesV2 = LoadHashesV2 (Files, (BEncodedDictionary) keypair.Value, PieceLength);
                        break;

                    case ("url-list"):
                        if (keypair.Value is BEncodedString httpSeedString) {
                            if (Uri.TryCreate (httpSeedString.Text, UriKind.Absolute, out Uri httpSeedUri)) {
                                HttpSeeds.Add (httpSeedUri);
                            }
                        } else if (keypair.Value is BEncodedList httpSeedList) {
                            foreach (BEncodedString str in httpSeedList)
                                if (Uri.TryCreate (str.Text, UriKind.Absolute, out Uri httpSeedUri)) {
                                    HttpSeeds.Add (httpSeedUri);
                                }
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        static IList<ITorrentFile> LoadTorrentFilesV1 (BEncodedList list, int pieceLength)
        {
            var sb = new StringBuilder (32);

            var files = new List<(string path, long length, ReadOnlyMemory<byte> md5sum, ReadOnlyMemory<byte> ed2k, ReadOnlyMemory<byte> sha1)> ();
            foreach (BEncodedDictionary dict in list) {
                long length = 0;
                string path = null;
                ReadOnlyMemory<byte> md5sum = default;
                ReadOnlyMemory<byte> ed2k = default;
                ReadOnlyMemory<byte> sha1 = default;

                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                    switch (keypair.Key.Text) {
                        case ("sha1"):
                            sha1 = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        case ("ed2k"):
                            ed2k = ((BEncodedString) keypair.Value).AsMemory ();
                            ;
                            break;

                        case ("length"):
                            length = long.Parse (keypair.Value.ToString ());
                            break;

                        case ("path.utf-8"):
                            foreach (BEncodedString str in ((BEncodedList) keypair.Value)) {
                                sb.Append (str.Text);
                                sb.Append (Path.DirectorySeparatorChar);
                            }
                            path = sb.ToString (0, sb.Length - 1);
                            sb.Remove (0, sb.Length);
                            break;

                        case ("path"):
                            if (string.IsNullOrEmpty (path)) {
                                foreach (BEncodedString str in ((BEncodedList) keypair.Value)) {
                                    sb.Append (str.Text);
                                    sb.Append (Path.DirectorySeparatorChar);
                                }
                                path = sb.ToString (0, sb.Length - 1);
                                sb.Remove (0, sb.Length);
                            }
                            break;
                        case ("md5sum"):
                            md5sum = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        default:
                            break; //FIXME: Log unknown values
                    }
                }

                PathValidator.Validate (path);
                files.Add ((path, length, md5sum, ed2k, sha1));
            }

            return Array.AsReadOnly<ITorrentFile> (TorrentFile.Create (pieceLength, files.ToArray ()));
        }

        static PieceHashesV2 LoadHashesV2 (IList<ITorrentFile> files, BEncodedDictionary hashes, int actualPieceLength)
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++) {
                var file = files[fileIndex];
                if (file.Length < actualPieceLength)
                    continue;

                if (!hashes.TryGetValue (BEncodedString.FromMemory (file.PiecesRoot), out BEncodedValue hashValue))
                    throw new TorrentException ($"the 'piece layers' dictionary did not contain an entry for the file '{file.Path}'");
                if (!(hashValue is BEncodedString hash))
                    throw new TorrentException ("The 'piece layers' dictionary should contain BEncodedStrings");

                if ((hash.Span.Length % 32) != 0)
                    throw new TorrentException ($"The piece layer for {file.Path} was not a valid array of SHA256 hashes");

                var src = hash.AsMemory ();
                using var _ = MemoryPool.Default.Rent (((src.Length + 63) / 64) * 32, out Memory<byte> dest);
                var pieceLength = actualPieceLength;
                while (src.Length != 32) {
                    for (int i = 0; i < src.Length / 64; i ++) {
                        hasher.AppendData (src.Slice (i * 64, 64));
                        if (!hasher.TryGetHashAndReset (dest.Slice (i * 32, 32).Span, out int written) || written != 32)
                            throw new TorrentException ($"Could not compute the SHA256 hash for file {file.Path}");
                    }
                    if (src.Length % 64 == 32) {
                        hasher.AppendData (src.Slice (src.Length - 32, 32));
                        hasher.AppendData (FinalLayerHash[pieceLength]);
                        if (!hasher.TryGetHashAndReset (dest.Slice (dest.Length - 32, 32).Span, out int written) || written != 32)
                            throw new TorrentException ($"Could not compute the SHA256 hash for file {file.Path}");
                    }
                    src = dest;
                    dest = dest.Slice (0, ((dest.Length + 63) / 64) * 32);
                    pieceLength *= 2;
                }
                if (!src.Span.SequenceEqual (file.PiecesRoot.Span))
                    throw new TorrentException ($"The has root is corrupt for file {file.Path}");
            }

            return new PieceHashesV2 (files, hashes);
        }

        static void LoadTorrentFilesV2 (string key, BEncodedDictionary data, List<ITorrentFile> files, int pieceLength, ref int totalPieces, string path)
        {
            if (key == "") {
                var length = ((BEncodedNumber) data["length"]).Number;
                if (length == 0) {
                    files.Add (new TorrentFile (path, length, 0, 0, 0));
                } else {
                    totalPieces++;
                    var piecesRoot = data.TryGetValue ("pieces root", out var value) ? ((BEncodedString) value).AsMemory () : ReadOnlyMemory<byte>.Empty;
                    files.Add (new TorrentFile (path, length, totalPieces, totalPieces + (int) (length / pieceLength), pieceLength * totalPieces, piecesRoot));
                    totalPieces = files.Last ().EndPieceIndex;
                }
            } else {
                foreach (var entry in data) {
                    LoadTorrentFilesV2 (entry.Key.Text, (BEncodedDictionary) entry.Value, files, pieceLength, ref totalPieces, Path.Combine (path, key));
                }
            }
        }

        static IList<ITorrentFile> LoadTorrentFilesV2 (BEncodedDictionary fileTree, int pieceLength)
        {
            var files = new List<ITorrentFile> ();
            int totalPieces = -1;
            foreach (var entry in fileTree)
                LoadTorrentFilesV2 (entry.Key.Text, (BEncodedDictionary) entry.Value, files, pieceLength, ref totalPieces, "");
            return Array.AsReadOnly (files.ToArray ());
        }
    }
}
