//
// EditableTorrent.cs
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
using System.Linq;

using MonoTorrent.BEncoding;

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
        static readonly BEncodedString NameKey = "name";
        private protected static readonly BEncodedString PieceLengthKey = "piece length";
        static readonly BEncodedString PrivateKey = "private";
        static readonly BEncodedString PublisherKey = "publisher";
        static readonly BEncodedString PublisherUrlKey = "publisher-url";

        public string? Announce {
            get => GetString (Metadata, AnnounceKey);
            set => SetString (Metadata, AnnounceKey, value);
        }

        public List<List<string>> Announces {
            get;
        }

        protected bool CanEditSecureMetadata {
            get; set;
        }

        public string? Comment {
            get => GetString (Metadata, CommentKey);
            set => SetString (Metadata, CommentKey, value);
        }

        public string? CreatedBy {
            get => GetString (Metadata, CreatedByKey);
            set => SetString (Metadata, CreatedByKey, value);
        }

        public string? Encoding {
            get => GetString (Metadata, EncodingKey);
            private set => SetString (Metadata, EncodingKey, value);
        }

        protected BEncodedDictionary InfoDict {
            get => GetDictionary (Metadata, InfoKey) ?? throw new InvalidOperationException ("The 'info' dictionary is unexpectedly missing");
            private set => SetDictionary (Metadata, InfoKey, value);
        }

        protected BEncodedDictionary Metadata {
            get; private set;
        }

        public string? Name {
            get => GetString (InfoDict, NameKey);
            set => SetString (InfoDict, NameKey, value);
        }

        public int PieceLength {
            get => (int) GetLong (InfoDict, PieceLengthKey);
            set => SetLong (InfoDict, PieceLengthKey, value);
        }

        public bool Private {
            get => GetLong (InfoDict, PrivateKey) == 1;
            set => SetLong (InfoDict, PrivateKey, value ? 1 : 0);
        }

        public string? Publisher {
            get => GetString (InfoDict, PublisherKey);
            set => SetString (InfoDict, PublisherKey, value);
        }

        public string? PublisherUrl {
            get => GetString (InfoDict, PublisherUrlKey);
            set => SetString (InfoDict, PublisherUrlKey, value);
        }

        protected EditableTorrent ()
            : this (new BEncodedDictionary ())
        {

        }

        protected EditableTorrent (BEncodedDictionary metadata)
        {
            Check.Metadata (metadata);
            Announces = new List<List<string>> ();
            Metadata = BEncodedValue.Clone (metadata);
            Initialise ();
        }

        void Initialise ()
        {
            if (!Metadata.ContainsKey (InfoKey))
                Metadata[InfoKey] = new BEncodedDictionary ();

            if (!Metadata.TryGetValue (AnnounceListKey, out BEncodedValue? value)) {
                value = new BEncodedList ();
                Metadata.Add (AnnounceListKey, value);
            }

            if (value is BEncodedList tiers)
                foreach (var tier in tiers.OfType<BEncodedList> ())
                    Announces.Add (tier.OfType<BEncodedString> ().Select (t => t.Text).ToList ());

            if (string.IsNullOrEmpty (Encoding))
                Encoding = "UTF-8";
        }

        protected void CheckCanEditSecure ()
        {
            if (!CanEditSecureMetadata)
                throw new InvalidOperationException ("Cannot edit metadata which alters the infohash while CanEditSecureMetadata is false");
        }

        public BEncodedValue? GetCustom (BEncodedString key)
        {
            if (Metadata.TryGetValue (key, out BEncodedValue? value))
                return value;
            return null;
        }

        public BEncodedValue? GetCustomSecure (BEncodedString key)
        {
            CheckCanEditSecure ();
            if (InfoDict.TryGetValue (key, out BEncodedValue? value))
                return value;
            return null;
        }

        public void SetCustom (BEncodedString key, BEncodedValue value)
        {
            Check.Key (key);
            Check.Value (value);

            if (InfoKey.Equals (key))
                CheckCanEditSecure ();
            Metadata[key] = value;
        }

        public void SetCustomSecure (BEncodedString key, BEncodedValue value)
        {
            CheckCanEditSecure ();

            Check.Key (key);
            Check.Value (value);
            InfoDict[key] = value;
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

        protected BEncodedDictionary? GetDictionary (BEncodedDictionary dictionary, BEncodedString key)
        {
            //            // Required? Probably.
            //            if (dictionary == InfoDict)
            //                CheckCanEditSecure ();

            if (dictionary.TryGetValue (key, out BEncodedValue? value))
                return (BEncodedDictionary) value;
            return null;
        }

        protected long GetLong (BEncodedDictionary dictionary, BEncodedString key)
        {
            if (dictionary.TryGetValue (key, out BEncodedValue? value))
                return ((BEncodedNumber) value).Number;
            throw new ArgumentException ($"The value for key {key} was not a BEncodedNumber");
        }

        protected string? GetString (BEncodedDictionary dictionary, BEncodedString key)
        {
            if (dictionary.TryGetValue (key, out BEncodedValue? value))
                return ((BEncodedString) value).Text;
            return null;
        }

        protected void SetDictionary (BEncodedDictionary dictionary, BEncodedString key, BEncodedDictionary value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary[key] = value;
        }

        protected void SetLong (BEncodedDictionary dictionary, BEncodedString key, long value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();
            dictionary[key] = new BEncodedNumber (value);
        }

        protected void SetString (BEncodedDictionary dictionary, BEncodedString key, string? value)
        {
            if (dictionary == InfoDict)
                CheckCanEditSecure ();

            if (value == null)
                dictionary.Remove (key);
            else
                dictionary[key] = new BEncodedString (value);
        }
    }
}
