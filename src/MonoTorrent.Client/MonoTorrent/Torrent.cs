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
        internal ReadOnlyMemory<byte> InfoMetadata { get; private set; }

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

            if (InfoHashes is null || InfoMetadata.IsEmpty)
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
        public IPieceHashes CreatePieceHashes (Dictionary<MerkleRoot, ReadOnlyMerkleTree> pieceHashes)
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
        /// <param name="hasV1Data"></param>
        /// <param name="hasV2Data"></param>
        void ProcessInfo (BEncodedDictionary dictionary, ref PieceHashesV1? hashesV1, ref bool hasV1Data, ref bool hasV2Data)
        {
            InfoMetadata = dictionary.Encode ();
            PieceLength = int.Parse (dictionary["piece length"].ToString ()!);
            hasV1Data = false;
            hasV2Data = false;

            if (dictionary.TryGetValue ("meta version", out BEncodedValue? metaVersion)) {
                if (metaVersion is BEncodedNumber metadataVersion) {
                    hasV2Data = metadataVersion.Number == 2;
                }
            }

            hasV1Data = dictionary.ContainsKey ("pieces");

            if (!hasV1Data && !hasV2Data)
                throw new TorrentException ("Unsupported torrent version detected.");

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
                        Source = keypair.Value.ToString () ?? "";
                        break;

                    case ("sha1"):
                        SHA1 = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("ed2k"):
                        ED2K = ((BEncodedString) keypair.Value).Span.ToArray ();
                        break;

                    case ("publisher-url.utf-8"):
                        PublisherUrl = ((BEncodedString) keypair.Value).Text;
                        break;

                    case ("publisher-url"):
                        if (string.IsNullOrEmpty (PublisherUrl))
                            PublisherUrl = ((BEncodedString) keypair.Value).Text;
                        break;

                    case ("publisher.utf-8"):
                        Publisher = keypair.Value.ToString () ?? "";
                        break;

                    case ("publisher"):
                        if (string.IsNullOrEmpty (Publisher))
                            Publisher = keypair.Value.ToString () ?? "";
                        break;

                    case ("files"):
                        // This is the list of files using the v1 torrent format.
                        v1Files = LoadTorrentFilesV1 ((BEncodedList) keypair.Value, PieceLength, hasV1Data && hasV2Data);
                        break;

                    case "file tree":
                        // This is the list of files using the v2 torrent format.
                        v2Files = LoadTorrentFilesV2 ((BEncodedDictionary) dictionary["file tree"], PieceLength, hasV1Data && hasV2Data);
                        break;

                    case ("name.utf-8"):
                        Name = keypair.Value.ToString () ?? "";
                        break;

                    case ("name"):
                        if (string.IsNullOrEmpty (Name))
                            Name = keypair.Value.ToString () ?? "";
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
            if (hasV1Data && v1Files.Count == 0 && !(hashesV1 is null))   // Not a multi-file v1 torrent
            {
                long length = long.Parse (dictionary["length"].ToString ()!);
                string path = Name;
                int endPiece = Math.Min (hashesV1.Count - 1, (int) ((length + (PieceLength - 1)) / PieceLength));
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

                    if (v1File.Padding != v2File.Padding) {
                        // BEP47 says padding is there so the *subsequent* file aligns with a piece start boundary.
                        // By a literal reading, and in line with the rest of the bittorrent spec, the last file
                        // can and should be considered the 'end' of the torrent (obviously :p) and so does not
                        // have a subsequent file, and so does not need padding. Similar to how blocks are requested
                        // in 16kB chunks, except for the final block which is just wahtever bytes are left over.
                        //
                        // Requested a clarification on the BEP. However both variants will need to be supported
                        // regardless of what the spec says because both are in the wild.
                        // Issue: https://github.com/bittorrent/bittorrent.org/issues/160
                        //
                        // If padding is mandatory for the last file, then remove the code which strips it out
                        // inside 'LoadTorrentFilesV2'.
                        if (v1File == v1Files.Last () && v2File == v2Files.Last ()) {
                            var mutableFiles = v2Files.ToList ();
                            mutableFiles[v2Files.Count - 1] = new TorrentFile (v2File.Path, v2File.Length, v2File.StartPieceIndex, v2File.EndPieceIndex, v2File.OffsetInTorrent, v2File.PiecesRoot, TorrentFileAttributes.None, v1File.Padding);
                            v2Files = Array.AsReadOnly (mutableFiles.ToArray ());
                        } else
                            throw new TorrentException ("Inconsistent hybrid torrent, file padding mismatch.");
                    }
                }

                Files = v2Files;
            } else if (hasV1Data) {
                Files = v1Files;
            } else if (hasV2Data) {
                Files = v2Files;
            }

            Size = Files.Select (f => f.Length + f.Padding).Sum ();
            if (Size == 0)
                throw new InvalidOperationException ("This torrent does not contain any files with non-zero length. There's nothing to download.");
        }

        void LoadInternal (BEncodedDictionary torrentInformation, RawInfoHashes infoHashes)
        {
            Check.TorrentInformation (torrentInformation);
            AnnounceUrls = new List<IList<string>> ().AsReadOnly ();

            bool hasV1Data = false;
            bool hasV2Data = false;
            PieceHashesV1? hashesV1 = null;
            PieceHashesV2? hashesV2 = null;
            foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in torrentInformation) {
                if (keypair.Value is BEncodedString && keypair.Value.ToString () == String.Empty)
                    continue;

                switch (keypair.Key.Text) {
                    case ("announce"):
                        // Ignore this if we have an announce-list
                        if (torrentInformation.ContainsKey ("announce-list"))
                            break;
                        AnnounceUrls = new List<IList<string>> {
                            new List<string> { keypair.Value.ToString () ?? "" }.Where (t => !string.IsNullOrEmpty(t)).ToList ().AsReadOnly ()
                        }.AsReadOnly ();
                        break;

                    case ("creation date"):
                        try {
                            try {
                                CreationDate = UnixEpoch.AddSeconds (long.Parse (keypair.Value.ToString () ?? ""));
                            } catch (Exception e) {
                                if (e is ArgumentOutOfRangeException)
                                    CreationDate = UnixEpoch.AddMilliseconds (long.Parse (keypair.Value.ToString () ?? ""));
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
                        Comment = keypair.Value.ToString () ?? "";       // Always take the UTF-8 version
                        break;                                          // even if there's an existing value

                    case ("comment"):
                        if (string.IsNullOrEmpty (Comment))
                            Comment = keypair.Value.ToString () ?? "";
                        break;

                    case ("publisher-url.utf-8"):                       // Always take the UTF-8 version
                        PublisherUrl = ((BEncodedString) keypair.Value).Text;      // even if there's an existing value
                        break;

                    case ("publisher-url"):
                        if (string.IsNullOrEmpty (PublisherUrl))
                            PublisherUrl = ((BEncodedString) keypair.Value).Text;
                        break;

                    case ("created by"):
                        CreatedBy = keypair.Value.ToString () ?? "";
                        break;

                    case ("encoding"):
                        Encoding = keypair.Value.ToString () ?? "";
                        break;

                    case ("info"):
                        ProcessInfo (((BEncodedDictionary) keypair.Value), ref hashesV1, ref hasV1Data, ref hasV2Data);
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
                                    tier.Add (((BEncodedString) bencodedTier[k]).Text);

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

                        var merkleTrees = dict.ToDictionary (
                            key => MerkleRoot.FromMemory (key.Key.AsMemory ()),
                            kvp => ReadOnlyMerkleTree.FromLayer (PieceLength, ((BEncodedString) kvp.Value).Span, MerkleRoot.FromMemory (kvp.Key.AsMemory ()))
                        );

                        hashesV2 = LoadHashesV2 (Files, merkleTrees, PieceLength);
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

            if (!hasV1Data && !hasV2Data)
                throw new NotSupportedException ("The supplied torrent did not contain BitTorrent V1 or BitTorrent V2 metadata.");

            // If all files are 1 piece long, then their root hash is all we need. Create the hashes object now!
            if (hashesV2 == null && Files.All (t => t.StartPieceIndex == t.EndPieceIndex))
                hashesV2 = LoadHashesV2 (Files, new Dictionary<MerkleRoot, ReadOnlyMerkleTree> (), PieceLength);

            InfoHashes = new InfoHashes (hasV1Data ? InfoHash.FromMemory (infoHashes.SHA1) : default, hasV2Data ? InfoHash.FromMemory (infoHashes.SHA256) : default);
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

        static IList<ITorrentFile> LoadTorrentFilesV1 (BEncodedList list, int pieceLength, bool isHybridTorrent)
        {
            var sb = new StringBuilder (32);

            var files = new List<TorrentFileTuple> ();
            foreach (BEncodedDictionary dict in list) {
                var tup = new TorrentFileTuple ();

                foreach (KeyValuePair<BEncodedString, BEncodedValue> keypair in dict) {
                    switch (keypair.Key.Text) {
                        case ("attr"):
                            tup.attributes = AttrStringToAttributesEnum (((BEncodedString) keypair.Value).Text);
                            break;

                        case ("sha1"):
                            tup.sha1 = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        case ("ed2k"):
                            tup.ed2k = ((BEncodedString) keypair.Value).AsMemory ();
                            break;

                        case ("length"):
                            tup.length = ((BEncodedNumber) keypair.Value).Number;
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

                // If this is *not* a padding file, ensure it is sorted alphabetically higher than the last non-padding file
                // when loading a hybrid torrent.
                // 
                // By BEP52 spec, hybrid torrents Hybrid torrents have padding files inserted between each file, and so must
                // have a fixed hash order to guarantee that the set up finrequired to have strictly alphabetical file ordering so
                // the v1 hashes are guaranteed to match  after padding files are inserted.
                PathValidator.Validate (tup.path);
                var lastNonPaddingFile = files.FindLast (t => !t.attributes.HasFlag (TorrentFileAttributes.Padding) && t.length > 0);
                if (isHybridTorrent && !tup.attributes.HasFlag (TorrentFileAttributes.Padding) && lastNonPaddingFile != null && StringComparer.Ordinal.Compare (tup.path, lastNonPaddingFile.path) < 0)
                    throw new TorrentException ("The list of files must be in strict alphabetical order in a hybrid torrent");
                files.Add (tup);
            }

            return Array.AsReadOnly<ITorrentFile> (TorrentFile.Create (pieceLength, files.ToArray ()));
        }

        static PieceHashesV2 LoadHashesV2 (IList<ITorrentFile> files, Dictionary<MerkleRoot, ReadOnlyMerkleTree> hashes, int pieceLength)
        {
            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++) {
                var file = files[fileIndex];
                if (file.Length <= pieceLength)
                    continue;

                if (!hashes.TryGetValue (file.PiecesRoot, out ReadOnlyMerkleTree? fileHash))
                    throw new TorrentException ($"The hash root is missing for file {file.Path}");
                if (!fileHash.Root.Span.SequenceEqual (file.PiecesRoot.Span))
                    throw new TorrentException ($"The hash root is corrupt for file {file.Path}");
            }
            return new PieceHashesV2 (pieceLength, files, hashes);
        }

        static void LoadTorrentFilesV2 (string key, BEncodedDictionary data, List<ITorrentFile> files, int pieceLength, ref int totalPieces, string path, bool isHybrid)
        {
            if (key == "") {
                var length = ((BEncodedNumber) data["length"]).Number;
                if (length == 0) {
                    files.Insert (0, new TorrentFile (path, length, 0, 0, 0, TorrentFileAttributes.None, 0));
                } else {
                    totalPieces++;
                    var offsetInTorrent = (files.LastOrDefault ()?.OffsetInTorrent ?? 0) + (files.LastOrDefault ()?.Length ?? 0) + (files.LastOrDefault ()?.Padding ?? 0);
                    var piecesRoot = data.TryGetValue ("pieces root", out var value) ? MerkleRoot.FromMemory (((BEncodedString) value).AsMemory ()) : MerkleRoot.Empty;

                    // A v2 only torrent *never* has padding. However, a hybrid v1/v2 torrent
                    // will *always* have padding as the v1 metadata will have padding files.
                    // We insert this padding in the v2 metadata so ITorrentFiles parsed using
                    // v1 and v2 metadata result in identical objects. Peers unaware of padding
                    // files may still request the bytes, so it's important to propagate this.
                    int padding = 0;
                    if (isHybrid && length % pieceLength != 0)
                        padding = (int) (pieceLength - (length % pieceLength));

                    files.Add (new TorrentFile (path,
                        length,
                        totalPieces,
                        totalPieces + (int) ((length - 1) / pieceLength),
                        offsetInTorrent,
                        piecesRoot,
                        TorrentFileAttributes.None,
                        padding));
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

            TorrentFile.Sort (files);

            // padding of last non-empty file must be 0.
            // There may not be any non-empty files, though that'd be a weird torrent :P
            var lastIndex = files.FindLastIndex (f => f.Length > 0);
            if (lastIndex != -1) {
                var last = files[lastIndex];
                files[lastIndex] = new TorrentFile (last.Path, last.Length, last.StartPieceIndex, last.EndPieceIndex, last.OffsetInTorrent, last.PiecesRoot, TorrentFileAttributes.None, 0);
            }
            return Array.AsReadOnly (files.ToArray ());
        }
    }
}
