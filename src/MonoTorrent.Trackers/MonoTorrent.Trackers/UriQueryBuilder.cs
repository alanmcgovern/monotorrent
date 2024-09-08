//
// UriQueryBuilder.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2009 Olivier Dufour
//                    Alan McGovern
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
using System.Diagnostics;
using System.Web;

namespace MonoTorrent.Trackers
{
    [DebuggerDisplay ("{" + nameof (ToUri) + " ()}")]
    class UriQueryBuilder
    {
        public static string UrlEncodeQuery (Span<byte> data)
            => UrlEncodeQuery ((ReadOnlySpan<byte>) data);

        public static string UrlEncodeQuery (ReadOnlySpan<byte> data)
            => HttpUtility.UrlEncode (data.ToArray ()).Replace ("+", "%20");

        readonly UriBuilder builder;
        readonly Dictionary<string, string> queryParams;

        public UriQueryBuilder (string uri)
            : this (new Uri (uri))
        {

        }

        public string this[string key] {
            get => queryParams[key];
            set => queryParams[key] = value;
        }

        public UriQueryBuilder (Uri uri)
        {
            builder = new UriBuilder (uri);
            queryParams = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
            ParseParameters ();
        }

        public UriQueryBuilder Add (string key, object value)
        {
            if (key == null)
                throw new ArgumentNullException (nameof (key));
            if (value == null)
                throw new ArgumentNullException (nameof (value));

            queryParams[key] = value.ToString () ?? throw new ArgumentException("Calling ToString on the object should not be null", nameof (value));
            return this;
        }

        public bool Contains (string key)
        {
            return queryParams.ContainsKey (key);
        }

        void ParseParameters ()
        {
            if (builder.Query.Length == 0 || !builder.Query.StartsWith ("?"))
                return;

            string[] strs = builder.Query.Remove (0, 1).Split ('&');
            for (int i = 0; i < strs.Length; i++) {
                string[] kv = strs[i].Split ('=');
                if (kv.Length == 2)
                    queryParams.Add (kv[0].Trim (), kv[1].Trim ());
            }
        }

        public Uri ToUri ()
        {
            string result = "";
            foreach (KeyValuePair<string, string> keypair in queryParams)
                result += $"{keypair.Key}={keypair.Value}&";
            builder.Query = result.Length == 0 ? result : result.Remove (result.Length - 1);
            return builder.Uri;
        }
    }
}
