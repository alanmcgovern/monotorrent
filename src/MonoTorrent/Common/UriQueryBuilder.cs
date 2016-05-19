using System;
using System.Collections.Generic;

namespace MonoTorrent.Common
{
    public class UriQueryBuilder
    {
        private readonly UriBuilder builder;
        private readonly Dictionary<string, string> queryParams;

        public UriQueryBuilder(string uri)
            : this(new Uri(uri))

        {
        }

        public UriQueryBuilder(Uri uri)
        {
            builder = new UriBuilder(uri);
            queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ParseParameters();
        }

        public string this[string key]
        {
            get { return queryParams[key]; }
            set { queryParams[key] = value; }
        }

        public UriQueryBuilder Add(string key, object value)
        {
            Check.Key(key);
            Check.Value(value);

            queryParams[key] = value.ToString();
            return this;
        }

        public bool Contains(string key)
        {
            return queryParams.ContainsKey(key);
        }

        private void ParseParameters()
        {
            if (builder.Query.Length == 0 || !builder.Query.StartsWith("?"))
                return;

            var strs = builder.Query.Remove(0, 1).Split('&');
            for (var i = 0; i < strs.Length; i++)
            {
                var kv = strs[i].Split('=');
                if (kv.Length == 2)
                    queryParams.Add(kv[0].Trim(), kv[1].Trim());
            }
        }

        public override string ToString()
        {
            return ToUri().OriginalString;
        }

        public Uri ToUri()
        {
            var result = "";
            foreach (var keypair in queryParams)
                result += keypair.Key + "=" + keypair.Value + "&";
            builder.Query = result.Length == 0 ? result : result.Remove(result.Length - 1);
            return builder.Uri;
        }
    }
}