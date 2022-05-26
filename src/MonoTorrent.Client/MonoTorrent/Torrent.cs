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
using System.Diagnostics.CodeAnalysis;
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
        internal static bool SupportsV2Torrents = true;
        internal static bool SupportsV1V2Torrents = false;

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
        static Torrent Load (Stream? stream, ReadOnlySpan<byte> buffer)
        {
            try {
                (var torrentDict, var infoHashes) = stream is null ? BEncodedDictionary.DecodeTorrent (buffer) : BEncodedDictionary.DecodeTorrent (stream);
                return new Torrent (torrentDict, infoHashes);
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
        public static bool TryLoad (string path, [NotNullWhen (true)] out Torrent? torrent)
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
        public static bool TryLoad (ReadOnlySpan<byte> span, [NotNullWhen (true)] out Torrent? torrent)
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
        public static bool TryLoad (Stream stream, [NotNullWhen (true)] out Torrent? torrent)
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

        public InfoHashes InfoHashes { get; private set; }

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
        public int PieceCount => Files[Files.Count - 1].EndPieceIndex + 1;

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

        PieceHashesV1? PieceHashesV1 { get; set; }
        PieceHashesV2? PieceHashesV2 { get; set; }

        Torrent (BEncodedDictionary torrentInformation, RawInfoHashes infoHashes)
        {
            AnnounceUrls = Array.Empty<IList<string>> ();
            Comment = string.Empty;
            CreatedBy = string.Empty;
            CreationDate = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Encoding = string.Empty;
            Files = Array.Empty<ITorrentFile> ();
            Name = string.Empty;
            Nodes = new BEncodedList ();
            Publisher = string.Empty;
            PublisherUrl = string.Empty;
            Source = "";
            HttpSeeds = new List<Uri> ();

            LoadInternal (torrentInformation, infoHashes);

            if (InfoHashes is null || InfoMetadata is null)
                throw new InvalidOperationException ("One of the required properties was unset after deserializing torrent metadata");
        }


        /// <summary>
        /// This is the array of SHA1 piece hashes contained within the torrent. Used to validate torrents which comply with the V1 specification.
        /// </summary>
        public IPieceHashes CreatePieceHashes ()
            => new PieceHashes (PieceHashesV1, PieceHashesV2);

        /// <summary>
        /// This is the array of SHA1 piece hashes contained within the torrent. Used to validate torrents which comply with the V1 specification.
        /// </summary>
        public IPieceHashes CreatePieceHashes (Dictionary<BEncodedString, BEncodedString> pieceHashes)
            => new PieceHashes (PieceHashesV1, LoadHashesV2 (Files, pieceHashes, PieceLength));

        public override bool Equals (object? obj)
            => Equals (obj as Torrent);

        public bool Equals (Torrent? other)
            => InfoHashes == other?.InfoHashes;

        public override int GetHashCode ()
            => InfoHashes.GetHashCode ();

        public override string ToString ()
            => Name;

        /// <summary>
        /// This method is called internally to load the information found within the "Info" section
        /// of the .torrent file
        /// </summary>
        /// <param name="dictionary">The dictionary representing the Info section of the .torrent file</param>
        /// <param name="hashesV1"></param>
        void ProcessInfo (BEncodedDictionary dictionary, ref PieceHashesV1? hashesV1)
        {
            InfoMetadata = dictionary.Encode ();
            PieceLength = int.Parse (dictionary["piece length"].ToString ()!);
            bool hasV1Data = false;
            bool hasV2Data = false;

            if (dictionary.TryGetValue ("meta version", out BEncodedValue? metaVersion)) {
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
                hashesV1 = new PieceHashesV1 (data, 20);
            }

            IList<ITorrentFile> v1Files = Array.Empty<ITorrentFile> ();
            IList<ITorrentFile> v2Files = Array.Empty<ITorrentFile> ();

            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dictionary) {
                switch (keypair.Key.Text) {
                    case ("source"):
                        Source = keypair.Value.ToString ()!;
                        break;

                    case ("sha1"):
                        SHA1 = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("ed2k"):
                        ED2K = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("publisher-url.utf-8"):
                        if (keypair.Value.ToString ()!.Length > 0)
                            PublisherUrl = keypair.Value.ToString ()!;
                        break;

                    case ("publisher-url"):
                        if ((string.IsNullOrEmpty (PublisherUrl)) && (keypair.Value.ToString ()!.Length > 0))
                            PublisherUrl = keypair.Value.ToString ()!;
                        break;

                    case ("publisher.utf-8"):
                        if (keypair.Value.ToString ()!.Length > 0)
                            Publisher = keypair.Value.ToString ()!;
                        break;

                    case ("publisher"):
                        if ((string.IsNullOrEmpty (Publisher)) && (keypair.Value.ToString ()!.Length > 0))
                            Publisher = keypair.Value.ToString ()!;
                        break;

                    case ("files"):
                        // This is the list of files using the v1 torrent format.
                        v1Files = LoadTorrentFilesV1 ((BEncodedList) keypair.Value, PieceLength);
                        break;

                    case "file tree":
                        // This is the list of files using the v2 torrent format.
                        v2Files = LoadTorrentFilesV2 ((BEncodedDictionary) dictionary["file tree"], PieceLength, hasV1Data && hasV2Data);
                        break;

                    case ("name.utf-8"):
                        if (keypair.Value.ToString ()!.Length > 0)
                            Name = keypair.Value.ToString ()!;
                        break;

                    case ("name"):
                        if ((string.IsNullOrEmpty (Name)) && (keypair.Value.ToString ()!.Length > 0))
                            Name = keypair.Value.ToString ()!;
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

            // fixup single file v1 file list
            if (hasV1Data && v1Files.Count == 0)   // Not a multi-file v1 torrent
            {
                long length = long.Parse (dictionary["length"].ToString ()!);
                string path = Name;
                int endPiece = Math.Min (hashesV1!.Count - 1, (int) ((length + (PieceLength - 1)) / PieceLength));
                v1Files = Array.AsReadOnly<ITorrentFile> (new[] { new TorrentFile (path, length, 0, endPiece, 0, TorrentFileAttributes.None, 0) });
            }

            if (hasV1Data && hasV2Data) {
                // check consistency between v1 and v2 file lists on hybrid torrents

                if (v1Files.Count != v2Files.Count)
                    throw new TorrentException ("Inconsistent hybrid torrent, number of files differ.");

                foreach (var (v1File, v2File) in v1Files.Zip (v2Files, (x, y) => (x, y))) {

                    if (v1File.Path != v2File.Path)
                        throw new TorrentException ("Inconsistent hybrid torrent, mismatch in v1 and v2 files.");

                    if (v1File.Length != v2File.Length)
                        throw new TorrentException ("Inconsistent hybrid torrent, file length mismatch.");

                    if (v1File.Padding != v2File.Padding)
                        throw new TorrentException ("Inconsistent hybrid torrent, file padding mismatch.");
                }

                Files = v2Files;
            } else if (hasV1Data) {
                Files = v1Files;
            } else if (hasV2Data) {
                Files = v2Files;
            }

            Size = Files.Select (f => f.Length).Sum ();
        }

        void LoadInternal (BEncodedDictionary torrentInformation, RawInfoHashes infoHashes)
        {
            Check.TorrentInformation (torrentInformation);
            AnnounceUrls = new List<IList<string>> ().AsReadOnly ();

            PieceHashesV1? hashesV1 = null;
            PieceHashesV2? hashesV2 = null;
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in torrentInformation) {
                switch (keypair.Key.Text) {
                    case ("announce"):
                        // Ignore this if we have an announce-list
                        if (torrentInformation.ContainsKey ("announce-list"))
                            break;
                        AnnounceUrls = new List<IList<string>> {
                            new List<string> { keypair.Value.ToString ()! }.AsReadOnly ()
                        }.AsReadOnly ();
                        break;

                    case ("creation date"):
                        try {
                            try {
                                CreationDate = UnixEpoch.AddSeconds (long.Parse (keypair.Value.ToString ()!));
                            } catch (Exception e) {
                                if (e is ArgumentOutOfRangeException)
                                    CreationDate = UnixEpoch.AddMilliseconds (long.Parse (keypair.Value.ToString ()!));
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
                        if (keypair.Value.ToString ()!.Length != 0)
                            Comment = keypair.Value.ToString ()!;       // Always take the UTF-8 version
                        break;                                          // even if there's an existing value

                    case ("comment"):
                        if (string.IsNullOrEmpty (Comment))
                            Comment = keypair.Value.ToString ()!;
                        break;

                    case ("publisher-url.utf-8"):                       // Always take the UTF-8 version
                        PublisherUrl = keypair.Value.ToString ()!;      // even if there's an existing value
                        break;

                    case ("publisher-url"):
                        if (string.IsNullOrEmpty (PublisherUrl))
                            PublisherUrl = keypair.Value.ToString ()!;
                        break;

                    case ("created by"):
                        CreatedBy = keypair.Value.ToString ()!;
                        break;

                    case ("encoding"):
                        Encoding = keypair.Value.ToString ()!;
                        break;

                    case ("info"):
                        ProcessInfo (((BEncodedDictionary) keypair.Value), ref hashesV1);
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
                                    tier.Add (bencodedTier[k].ToString ()!);

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
                        var dict = (BEncodedDictionary) keypair.Value;
                        hashesV2 = LoadHashesV2 (Files, dict.ToDictionary (t => t.Key, t => (BEncodedString) t.Value), PieceLength);
                        break;

                    case ("url-list"):
                        if (keypair.Value is BEncodedString httpSeedString) {
                            if (Uri.TryCreate (httpSeedString.Text, UriKind.Absolute, out Uri? httpSeedUri)) {
                                HttpSeeds.Add (httpSeedUri);
                            }
                        } else if (keypair.Value is BEncodedList httpSeedList) {
                            foreach (BEncodedString str in httpSeedList)
                                if (Uri.TryCreate (str.Text, UriKind.Absolute, out Uri? httpSeedUri)) {
                                    HttpSeeds.Add (httpSeedUri);
                                }
                        }
                        break;

                    default:
                        break;
                }
            }

            if (SupportsV2Torrents && SupportsV1V2Torrents) {
                InfoHashes = new InfoHashes (hashesV1 == null ? default : InfoHash.FromMemory (infoHashes.SHA1), Files[0].PiecesRoot.IsEmpty ? default : InfoHash.FromMemory (infoHashes.SHA256));
            } else if (SupportsV2Torrents) {
                if (Files[0].PiecesRoot.IsEmpty)
                    InfoHashes = new InfoHashes (hashesV1 == null ? default : InfoHash.FromMemory (infoHashes.SHA1), default);
                else
                    InfoHashes = new InfoHashes (default, InfoHash.FromMemory (infoHashes.SHA256));
            } else {
                InfoHashes = new InfoHashes (InfoHash.FromMemory (infoHashes.SHA1), default);
            }
            PieceHashesV1 = InfoHashes.V1 is null ? null : hashesV1;
            PieceHashesV2 = InfoHashes.V2 is null ? null : hashesV2;
        }

        static TorrentFileAttributes AttrStringToAttributesEnum (string attr)
        {
            var result = TorrentFileAttributes.None;

            if (attr.Contains ("l"))
                result |= TorrentFileAttributes.Symlink;

            if (attr.Contains ("x"))
                result |= TorrentFileAttributes.Executable;

            if (attr.Contains ("h"))
                result |= TorrentFileAttributes.Hidden;

            if (attr.Contains ("p"))
                result |= TorrentFileAttributes.Padding;

            return result;
        }

        static IList<ITorrentFile> LoadTorrentFilesV1 (BEncodedList list, int pieceLength)
        {
            var sb = new StringBuilder (32);

            var files = new List<TorrentFileTuple> ();
            foreach (BEncodedDictionary dict in list) {
                var tup = new TorrentFileTuple ();

                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                    switch (keypair.Key.Text) {
                        case ("attr"):
                            tup.attributes = AttrStringToAttributesEnum (keypair.Value.ToString ()!);
                            break;

                        case ("sha1"):
                            tup.sha1 = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        case ("ed2k"):
                            tup.ed2k = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        case ("length"):
                            tup.length = long.Parse (keypair.Value.ToString ()!);
                            break;

                        case ("path.utf-8"):
                            foreach (BEncodedString str in ((BEncodedList) keypair.Value)) {
                                if (!BEncodedString.IsNullOrEmpty (str)) {
                                    sb.Append (str.Text);
                                    sb.Append (Path.DirectorySeparatorChar);
                                }
                            }
                            tup.path = sb.ToString (0, sb.Length - 1);
                            sb.Remove (0, sb.Length);
                            break;

                        case ("path"):
                            if (string.IsNullOrEmpty (tup.path)) {
                                foreach (BEncodedString str in ((BEncodedList) keypair.Value)) {
                                    if (!BEncodedString.IsNullOrEmpty (str)) {
                                        sb.Append (str.Text);
                                        sb.Append (Path.DirectorySeparatorChar);
                                    }
                                }
                                tup.path = sb.ToString (0, sb.Length - 1);
                                sb.Remove (0, sb.Length);
                            }
                            break;

                        case ("md5sum"):
                            tup.md5sum = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        default:
                            break; //FIXME: Log unknown values
                    }
                }
                if (tup.path == null)
                    // FIXME: Log invalid paths somewhere?
                    continue;

                PathValidator.Validate (tup.path);
                files.Add (tup);
            }

            return Array.AsReadOnly<ITorrentFile> (TorrentFile.Create (pieceLength, files.ToArray ()));
        }

        static PieceHashesV2 LoadHashesV2 (IList<ITorrentFile> files, Dictionary<BEncodedString, BEncodedString> hashes, int pieceLength)
        {
            using var hasher = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);

            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++) {
                var file = files[fileIndex];
                if (file.Length < pieceLength)
                    continue;

                if (!hashes.TryGetValue (BEncodedString.FromMemory (file.PiecesRoot), out BEncodedString? hashValue))
                    throw new TorrentException ($"the 'piece layers' dictionary did not contain an entry for the file '{file.Path}'");
                if (!(hashValue is BEncodedString hash))
                    throw new TorrentException ("The 'piece layers' dictionary should contain BEncodedStrings");

                if ((hash.Span.Length % 32) != 0)
                    throw new TorrentException ($"The piece layer for {file.Path} was not a valid array of SHA256 hashes");

                Span<byte> computedHash = stackalloc byte[32];
                if (!MerkleHash.TryHash (hasher, hash.AsMemory (), pieceLength, computedHash, out int written) || written != 32)
                    throw new InvalidOperationException ($"Could not compute merkle hash for file '{file.Path}'");

                if (!computedHash.SequenceEqual (file.PiecesRoot.Span))
                    throw new TorrentException ($"The has root is corrupt for file {file.Path}");
            }

            return new PieceHashesV2 (files, hashes);
        }

        static void LoadTorrentFilesV2 (string key, BEncodedDictionary data, List<ITorrentFile> files, int pieceLength, ref int totalPieces, string path, bool isHybrid)
        {
            if (key == "") {
                var length = ((BEncodedNumber) data["length"]).Number;
                if (length == 0) {
                    files.Add (new TorrentFile (path, length, 0, 0, 0, TorrentFileAttributes.None, 0));
                } else {
                    totalPieces++;
                    var offsetInTorrent = (files.LastOrDefault ()?.OffsetInTorrent ?? 0) + (files.LastOrDefault ()?.Length ?? 0) + (files.LastOrDefault ()?.Padding ?? 0);
                    var piecesRoot = data.TryGetValue ("pieces root", out var value) ? ((BEncodedString) value).AsMemory () : ReadOnlyMemory<byte>.Empty;

                    // TODO JMIK: wrong calculation for endIndex, when length is exactly pieceLength?
                    files.Add (new TorrentFile (path,
                        length,
                        totalPieces,
                        totalPieces + (int) ((length - 1) / pieceLength),
                        offsetInTorrent,
                        piecesRoot,
                        TorrentFileAttributes.None,
                        isHybrid ? pieceLength - length % pieceLength : 0));
                    totalPieces = files.Last ().EndPieceIndex;
                }
            } else {
                foreach (var entry in data) {
                    LoadTorrentFilesV2 (entry.Key.Text, (BEncodedDictionary) entry.Value, files, pieceLength, ref totalPieces, Path.Combine (path, key), isHybrid);
                }
            }
        }

        static IList<ITorrentFile> LoadTorrentFilesV2 (BEncodedDictionary fileTree, int pieceLength, bool isHybrid)
        {
            var files = new List<ITorrentFile> ();
            int totalPieces = -1;
            foreach (var entry in fileTree)
                LoadTorrentFilesV2 (entry.Key.Text, (BEncodedDictionary) entry.Value, files, pieceLength, ref totalPieces, "", isHybrid);

            // padding of last torrent must be 0.
            var last = files.Last ();
            files[files.Count - 1] = new TorrentFile (last.Path, last.Length, last.StartPieceIndex, last.EndPieceIndex, last.OffsetInTorrent, last.PiecesRoot, TorrentFileAttributes.None, 0);
            return Array.AsReadOnly (files.ToArray ());
        }
    }
}
