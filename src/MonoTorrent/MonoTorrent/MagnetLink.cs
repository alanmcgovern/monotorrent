
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent
{
    public class MagnetLink
    {
        /// <summary>
        /// The list of tracker Urls.
        /// </summary>
        public RawTrackerTier AnnounceUrls {
            get;
        }

        /// <summary>
        /// The infohash of the torrent.
        /// </summary>
        public InfoHash InfoHash {
            get;
        }

        /// <summary>
        /// The size in bytes of the data, if available.
        /// </summary>
        public long? Size {
            get;
        }

        /// <summary>
        /// The display name of the torrent, if available.
        /// </summary>
        public string Name {
            get;
        }

        /// <summary>
        /// The list of webseed Urls.
        /// </summary>
        public IList<string> Webseeds {
            get;
        }

        public MagnetLink (InfoHash infoHash, string name = null, IList<string> announceUrls = null, IEnumerable<string> webSeeds = null, long? size = null)
        {
            InfoHash = infoHash ?? throw new ArgumentNullException (nameof (infoHash));
            Name = name;
            AnnounceUrls = new RawTrackerTier (announceUrls ?? Array.Empty<string> ());
            Webseeds = new List<string> (webSeeds ?? Array.Empty<string> ()).AsReadOnly ();
            Size = size;
        }

        /// <summary>
        /// Parses a magnet link from the given string. The uri should be in the form magnet:?xt=urn:btih:
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static MagnetLink Parse (string uri)
            => FromUri (new Uri (uri));

        /// <summary>
        /// Parses a magnet link from the given Uri. The uri should be in the form magnet:?xt=urn:btih:
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static MagnetLink FromUri (Uri uri)
        {
            InfoHash infoHash = null;
            string name = null;
            var announceUrls = new RawTrackerTier ();
            var webSeeds = new List<string> ();
            long? size = null;

            if (uri.Scheme != "magnet")
                throw new FormatException ("Magnet links must start with 'magnet:'.");

            string[] parameters = uri.Query.Substring (1).Split ('&');
            for (int i = 0; i < parameters.Length ; i++)
            {
                string[] keyval = parameters[i].Split ('=');
                if (keyval.Length != 2)
                    throw new FormatException ("A field-value pair of the magnet link contain more than one equal'.");
                switch (keyval[0].Substring(0, 2))
                {
                    case "xt"://exact topic
                        if (infoHash != null)
                            throw new FormatException ("More than one infohash in magnet link is not allowed.");

                        string val = keyval[1].Substring(9);
                        switch (keyval[1].Substring(0, 9))
                        {
                            case "urn:sha1:"://base32 hash
                            case "urn:btih:":
                            if (val.Length == 32)
                                infoHash = InfoHash.FromBase32 (val);
                            else if (val.Length == 40)
                                infoHash = InfoHash.FromHex (val);
                            else
                                throw new FormatException("Infohash must be base32 or hex encoded.");
                            break;
                        }
                    break;
                    case "tr" ://address tracker
                        var urlBytes = UriHelper.UrlDecode(keyval[1]);
                        announceUrls.Add(Encoding.UTF8.GetString(urlBytes));
                    break;
                    case "as"://Acceptable Source
                        var webseedBytes = UriHelper.UrlDecode(keyval[1]);
                        webSeeds.Add(Encoding.UTF8.GetString (webseedBytes));
                    break;
                    case "dn"://display name
                        var nameBytes = UriHelper.UrlDecode(keyval[1]);
                        name = Encoding.UTF8.GetString(nameBytes);
                    break;
                    case "xl"://exact length
                        size = long.Parse (keyval [1]);
                    break;
                    case "xs":// eXact Source - P2P link.
                    case "kt"://keyword topic
                    case "mt"://manifest topic
                        //not supported for moment
                    break;
                    default:
                        //not supported
                    break;
                }
            }

            return new MagnetLink (infoHash, name, announceUrls, webSeeds, size);
        }

        public string ToV1Uri ()
            => ToUri (1);

        string ToUri (int formatVersion)
        {
            var sb = new StringBuilder ();
            sb.Append ("magnet:?");
            if (formatVersion == 1) {
                sb.Append ("xt=urn:btih:");
                sb.Append (InfoHash.ToHex ());
            } else if (formatVersion == 2) {
                sb.Append ("xt=urn:btmh");
                throw new NotSupportedException ("Need to add support for the new 'multihash' thing");
            } else {
                throw new NotSupportedException ();
            }

            if (!string.IsNullOrEmpty (Name)) {
                sb.Append ("&dn=");
                sb.Append (UriHelper.UrlEncode (Encoding.UTF8.GetBytes (Name)));
            }

            foreach (var tracker in AnnounceUrls) {
                sb.Append ("&tr=");
                sb.Append (UriHelper.UrlEncode (Encoding.UTF8.GetBytes (tracker)));
            }

            foreach (var webseed in Webseeds) {
                sb.Append ("&as=");
                sb.Append (UriHelper.UrlEncode (Encoding.UTF8.GetBytes (webseed)));
            }

            return sb.ToString ();
        }
    }
}
