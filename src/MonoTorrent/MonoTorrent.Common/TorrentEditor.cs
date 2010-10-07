//
// TorrentEditor.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2010 Alan McGovern
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

using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent {

    public class TorrentEditor {

        public string Announce {
            get { return GetString (Metadata, "announce"); }
            set { SetString (Metadata, "announce", value); }
        }

        public RawTrackerTiers Announces {
            get; private set;
        }

        public bool CanEditSecureMetadata {
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

        BEncodedDictionary InfoDict {
            get { return (BEncodedDictionary) Metadata ["info"]; }
        }

        BEncodedDictionary Metadata {
            get; set;
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
            LoadFrom (torrent.ToDictionary ());
        }

        public TorrentEditor (BEncodedDictionary metadata)
        {
            Check.Metadata (metadata);
            LoadFrom (BEncodedValue.Clone (metadata));
        }

        void LoadFrom (BEncodedDictionary metadata)
        {
            Metadata = metadata;
            BEncodedValue value;
            if (!Metadata.TryGetValue ("announce-list", out value)) {
                value = new BEncodedList ();
                Metadata.Add ("announce-list", value);
            }

            Announces = new RawTrackerTiers ((BEncodedList) value);
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
