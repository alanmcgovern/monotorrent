using System;

using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent
{
    public abstract class EditableTorrent
    {

        public string Announce {
            get { return GetString (Metadata, "announce"); }
            set { SetString (Metadata, "announce", value); }
        }

        public RawTrackerTiers Announces {
            get; private set;
        }

        protected bool CanEditSecureMetadata {
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

        protected BEncodedDictionary InfoDict {
            get { return GetDictionary (Metadata, "info"); }
            private set { SetDictionary (Metadata, "info", new BEncodedDictionary ()); }
        }

        protected BEncodedDictionary Metadata {
            get; private set;
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

        public EditableTorrent ()
            : this (new BEncodedDictionary ())
        {

        }

        public EditableTorrent (BEncodedDictionary metadata)
        {
            Metadata = metadata;

            BEncodedValue value;
            if (!Metadata.TryGetValue ("announce-list", out value)) {
                value = new BEncodedList ();
                Metadata.Add ("announce-list", value);
            }

            if (string.IsNullOrEmpty (Encoding))
                Encoding = "UTF-8";

            if (InfoDict == null)
                InfoDict = new BEncodedDictionary ();

            Announces = new RawTrackerTiers ((BEncodedList) value);
        }

        public void AddCustom (BEncodedString key, BEncodedValue value)
        {
            Check.Key (key);
            Check.Value (value);
            Metadata [key] = value;
        }

        public void AddCustomSecure (BEncodedString key, BEncodedValue value)
        {
            CheckCanEditSecure ();

            Check.Key (key);
            Check.Value (value);
            InfoDict [key] = value;
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
