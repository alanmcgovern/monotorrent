using System;

using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent
{
    public class TorrentEditor {

        public bool CanEditSecureMetadata {
            get; set;
        }

        BEncodedDictionary InfoDict {
            get { return (BEncodedDictionary) Metadata ["info"]; }
        }

        BEncodedDictionary Metadata {
            get; set;
        }

        public string Comment {
            get { return GetString (Metadata, "comment"); }
            set { SetString (Metadata, "comment", value); }
        }

        public string CreatedBy {
            get { return GetString (Metadata, "created by"); }
            set { SetString (Metadata, "created by", value); }
        }

        public string Encoding {
            get { return GetString (Metadata, "encoding"); }
            private set { SetString (Metadata, "encoding", value); }
        }

        public long PieceLength {
            get { return GetLong (InfoDict, "piece length"); }
            set { SetLong (InfoDict, "piece length", value); }
        }

        public bool Private {
            get { return GetLong (InfoDict, "private") == 1; }
            set { SetLong (InfoDict, "private", value ? 1 : 0); }
        }

        public string Publisher {
            get { return GetString (InfoDict, "publisher"); }
            set { SetString (InfoDict, "publisher", value); }
        }

        public string PublisherUrl {
            get { return GetString (InfoDict, "publisher-url"); }
            set { SetString (InfoDict, "publisher-url", value); }
        }

        public TorrentEditor (Torrent torrent)
        {
            Check.Torrent (torrent);
            Metadata = torrent.ToDictionary ();
        }

        public TorrentEditor (BEncodedDictionary metadata)
        {
            Check.Metadata (metadata);
            Metadata = (BEncodedDictionary) BEncodedValue.Decode (metadata.Encode ());
        }

        void CheckCanEditSecure ()
        {
            if (!CanEditSecureMetadata)
                throw new InvalidOperationException ("Cannot edit metadata which alters the infohash while CanEditSecureMetadata is false");
        }

        long GetLong (BEncodedDictionary dictionary, BEncodedString key)
        {
            BEncodedValue value;
            if (dictionary.TryGetValue (key, out value))
                return ((BEncodedNumber) value).Number;
            throw new ArgumentException (string.Format ("The value for key {0} was not a BEncodedNumber", key));
        }

        string GetString (BEncodedDictionary dictionary, BEncodedString key)
        {
            BEncodedValue value;
            if (dictionary.TryGetValue (key, out value))
                return value.ToString ();
            return "";
        }

        void SetLong (BEncodedDictionary dictionary, BEncodedString key, long value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary [key] = new BEncodedNumber (value);
        }

        void SetString (BEncodedDictionary dictionary, BEncodedString key, string value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary [key] = new BEncodedString (value);
        }

        public BEncodedDictionary ToDictionary ()
        {
            return BEncodedValue.Clone (Metadata);
        }

        public Torrent ToTorrent ()
        {
            return Torrent.Load (ToDictionary ());
        }
    }
}
