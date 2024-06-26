//
// MagnetLink.cs
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
using System.Text;

namespace MonoTorrent
{
    public class MagnetLink
    {
        /// <summary>
        /// The list of tracker Urls.
        /// </summary>
        public IList<string> AnnounceUrls {
            get;
        }

        /// <summary>
        /// The infohashes for this torrent.
        /// </summary>
        public InfoHashes InfoHashes { get; private set; }

        /// <summary>
        /// The size in bytes of the data, if available.
        /// </summary>
        public long? Size {
            get;
        }

        /// <summary>
        /// The display name of the torrent, if available.
        /// </summary>
        public string? Name {
            get;
        }

        /// <summary>
        /// The list of webseed Urls.
        /// </summary>
        public IList<string> Webseeds {
            get;
        }

        public MagnetLink (InfoHash infoHash, string? name = null, IList<string>? announceUrls = null, IEnumerable<string>? webSeeds = null, long? size = null)
            : this (InfoHashes.FromInfoHash (infoHash), name, announceUrls, webSeeds, size)
        {

        }

        public MagnetLink (InfoHashes infoHashes, string? name = null, IList<string>? announceUrls = null, IEnumerable<string>? webSeeds = null, long? size = null)
        {
            InfoHashes = infoHashes ?? throw new ArgumentNullException (nameof (infoHashes));

            Name = name;
            AnnounceUrls = new List<string> (announceUrls ?? Array.Empty<string> ()).AsReadOnly ();
            Webseeds = new List<string> (webSeeds ?? Array.Empty<string> ()).AsReadOnly ();
            Size = size;
        }

        /// <summary>
        /// Parses a magnet link from the given string. The uri should be in the form magnet:?xt=urn:btih:
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static MagnetLink Parse (string uri)
        {
            return FromUri (new Uri (uri));
        }

        /// <summary>
        /// Returns <see langword="true"/> if a bitorrent magnet link was successfully parsed from the given string. Otherwise
        /// return false.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="magnetLink"></param>
        /// <returns></returns>
        public static bool TryParse (string uri, [NotNullWhen(true)] out MagnetLink? magnetLink)
        {
            try {
                magnetLink = Parse (uri);
            } catch {
                magnetLink = null;
            }
            return magnetLink != null;
        }

        /// <summary>
        /// Parses a magnet link from the given Uri. The uri should be in the form magnet:?xt=urn:btih:
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static MagnetLink FromUri (Uri uri)
        {
            InfoHashes? infoHashes = null;
            string? name = null;
            var announceUrls = new List<string> ();
            var webSeeds = new List<string> ();
            long? size = null;

            if (uri.Scheme != "magnet")
                throw new FormatException ("Magnet links must start with 'magnet:'.");

            string[] parameters = uri.Query.Substring (1).Split ('&');
            for (int i = 0; i < parameters.Length; i++) {
                string[] keyval = parameters[i].Split ('=');
                if (keyval.Length != 2) {
                    // Skip anything we don't understand. Urls could theoretically contain many
                    // unknown parameters.
                    continue;
                }
                switch (keyval[0].Substring (0, 2)) {
                    case "xt"://exact topic
                        string val = keyval[1].Substring (9);
                        switch (keyval[1].Substring (0, 9)) {
                            case "urn:sha1:"://base32 hash
                            case "urn:btih:":
                                if (infoHashes?.V1 != null)
                                    throw new FormatException ("More than one v1 infohash in magnet link is not allowed.");

                                if (val.Length == 32)
                                    infoHashes = new InfoHashes (InfoHash.FromBase32 (val), infoHashes?.V2);
                                else if (val.Length == 40)
                                    infoHashes = new InfoHashes (InfoHash.FromHex (val), infoHashes?.V2);
                                else
                                    throw new FormatException ("Infohash must be base32 or hex encoded.");
                                break;

                            case "urn:btmh:":
                                if (infoHashes?.V2 != null)
                                    throw new FormatException ("More than one v2 multihash in magnet link is not allowed.");

                                // BEP52: Support v2 magnet links
                                infoHashes = new InfoHashes (infoHashes?.V1, InfoHash.FromMultiHash (val));
                                break;
                        }
                        break;
                    case "tr"://address tracker
                        announceUrls.Add (keyval[1].UrlDecodeUTF8 ());
                        break;
                    case "as"://Acceptable Source
                        webSeeds.Add (keyval[1].UrlDecodeUTF8 ());
                        break;
                    case "dn"://display name
                        name = keyval[1].UrlDecodeUTF8 ();
                        break;
                    case "xl"://exact length
                        size = long.Parse (keyval[1]);
                        break;
                    //case "xs":// eXact Source - P2P link.
                    //case "kt"://keyword topic
                    //case "mt"://manifest topic
                    // Unused
                    //break;
                    default:
                        // Unknown/unsupported
                        break;
                }
            }

            if (infoHashes == null)
                throw new FormatException ("The magnet link did not contain a valid 'xt' parameter referencing the infohash");

            return new MagnetLink (infoHashes, name, announceUrls, webSeeds, size);
        }

        public string ToV1String ()
        {
            return ConvertToString ();
        }

        public Uri ToV1Uri ()
        {
            return new Uri (ToV1String ());
        }

        string ConvertToString ()
        {
            var sb = new StringBuilder ();
            sb.Append ("magnet:?");
            sb.Append ("xt=urn:btih:");
            sb.Append (InfoHashes.V1OrV2.ToHex ());

            if (!string.IsNullOrEmpty (Name)) {
                sb.Append ("&dn=");
                sb.Append (Name.UrlEncodeQueryUTF8 ());
            }

            foreach (string tracker in AnnounceUrls) {
                sb.Append ("&tr=");
                sb.Append (tracker.UrlEncodeQueryUTF8 ());
            }

            foreach (string webseed in Webseeds) {
                sb.Append ("&as=");
                sb.Append (webseed.UrlEncodeQueryUTF8 ());
            }

            return sb.ToString ();
        }
    }
}
