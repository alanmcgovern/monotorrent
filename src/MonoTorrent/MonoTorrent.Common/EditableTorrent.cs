using System;
using System.Collections.Generic;

using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent
{
    public abstract class EditableTorrent
    {
        static readonly BEncodedString AnnounceKey = "announce";
        static readonly BEncodedString AnnounceListKey = "announce-list";
        static readonly BEncodedString CommentKey = "comment";
        static readonly BEncodedString CreatedByKey = "created by";
        static readonly BEncodedString EncodingKey = "encoding";
        static readonly BEncodedString InfoKey = "info";
        static readonly BEncodedString PieceLengthKey = "piece length";
        static readonly BEncodedString PrivateKey = "private";
        static readonly BEncodedString PublisherKey = "publisher";
        static readonly BEncodedString PublisherUrlKey = "publisher-url";

        public string Announce {
            get { return GetString (Metadata, AnnounceKey); }
            set { SetString (Metadata, AnnounceKey, value); }
        }

        public IList<RawTrackerTier> Announces {
            get; private set;
        }

        protected bool CanEditSecureMetadata {
            get; set;
        }

        public string Comment {
            get { return GetString (Metadata, CommentKey); }
            set { SetString (Metadata, CommentKey, value); }
        }

        public string CreatedBy {
            get { return GetString (Metadata, CreatedByKey); }
            set { SetString (Metadata, CreatedByKey, value); }
        }

        public string Encoding {
            get { return GetString (Metadata, EncodingKey); }
            private set { SetString (Metadata, EncodingKey, value); }
        }

        protected BEncodedDictionary InfoDict {
            get { return GetDictionary (Metadata, InfoKey); }
            private set { SetDictionary (Metadata, InfoKey, value); }
        }

        protected BEncodedDictionary Metadata {
            get; private set;
        }

        public long PieceLength {
            get { return GetLong (InfoDict, PieceLengthKey); }
            set { SetLong (InfoDict, PieceLengthKey, value); }
        }

        public bool Private {
            get { return GetLong (InfoDict, PrivateKey) == 1; }
            set { SetLong (InfoDict, PrivateKey, value ? 1 : 0); }
        }

        public string Publisher {
            get { return GetString (InfoDict, PublisherKey); }
            set { SetString (InfoDict, PublisherKey, value); }
        }

        public string PublisherUrl {
            get { return GetString (InfoDict, PublisherUrlKey); }
            set { SetString (InfoDict, PublisherUrlKey, value); }
        }

        public EditableTorrent ()
            : this (new BEncodedDictionary ())
        {

        }

        public EditableTorrent (Torrent torrent)
        {
            Check.Torrent (torrent);
            Initialise (torrent.ToDictionary ());
        }

        public EditableTorrent (BEncodedDictionary metadata)
        {
            Check.Metadata (metadata);
            Initialise (BEncodedValue.Clone (metadata));
        }

        void Initialise (BEncodedDictionary metadata)
        {
            Metadata = metadata;

            BEncodedValue value;
            if (!Metadata.TryGetValue (AnnounceListKey, out value)) {
                value = new BEncodedList ();
                Metadata.Add (AnnounceListKey, value);
            }
            Announces = new RawTrackerTiers ((BEncodedList) value);

            if (string.IsNullOrEmpty (Encoding))
                Encoding = "UTF-8";

            if (InfoDict == null)
                InfoDict = new BEncodedDictionary ();
        }

        protected void CheckCanEditSecure ()
        {
            if (!CanEditSecureMetadata)
                throw new InvalidOperationException ("Cannot edit metadata which alters the infohash while CanEditSecureMetadata is false");
        }

        public BEncodedValue GetCustom (BEncodedString key)
        {
            BEncodedValue value;
            if (Metadata.TryGetValue (key, out value))
                return value;
            return null;
        }

        public BEncodedValue GetCustomSecure (BEncodedString key)
        {
            CheckCanEditSecure ();
            BEncodedValue value;
            if (InfoDict.TryGetValue (key, out value))
                return value;
            return null;
        }

        public void SetCustom (BEncodedString key, BEncodedValue value)
        {
            Check.Key (key);
            Check.Value (value);

            if (InfoKey.Equals (key))
                CheckCanEditSecure ();
            Metadata [key] = value;
        }

        public void SetCustomSecure (BEncodedString key, BEncodedValue value)
        {
            CheckCanEditSecure ();

            Check.Key (key);
            Check.Value (value);
            InfoDict [key] = value;
        }

        public void RemoveCustom (BEncodedString key)
        {
            Check.Key (key);
            Metadata.Remove (key);
        }

        public void RemoveCustomSecure (BEncodedString key)
        {
            CheckCanEditSecure ();
            Check.Key (key);
            InfoDict.Remove (key);
        }

        public BEncodedDictionary ToDictionary ()
        {
            return BEncodedValue.Clone (Metadata);
        }

        public Torrent ToTorrent ()
        {
            return Torrent.Load (ToDictionary ());
        }

        protected BEncodedDictionary GetDictionary (BEncodedDictionary dictionary, BEncodedString key)
        {
//            // Required? Probably.
//            if (dictionary == InfoDict)
//                CheckCanEditSecure ();

            BEncodedValue value;
            if (dictionary.TryGetValue (key, out value))
                return (BEncodedDictionary) value;
            return null;
        }

        protected long GetLong (BEncodedDictionary dictionary, BEncodedString key)
        {
            BEncodedValue value;
            if (dictionary.TryGetValue (key, out value))
                return ((BEncodedNumber) value).Number;
            throw new ArgumentException (string.Format ("The value for key {0} was not a BEncodedNumber", key));
        }

        protected string GetString (BEncodedDictionary dictionary, BEncodedString key)
        {
            BEncodedValue value;
            if (dictionary.TryGetValue (key, out value))
                return value.ToString ();
            return "";
        }

        protected void SetDictionary (BEncodedDictionary dictionary, BEncodedString key, BEncodedDictionary value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary [key] = value;
        }

        protected void SetLong (BEncodedDictionary dictionary, BEncodedString key, long value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary [key] = new BEncodedNumber (value);
        }

        protected void SetString (BEncodedDictionary dictionary, BEncodedString key, string value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary [key] = new BEncodedString (value);
        }
    }
}
