//
// ScrapeRequest.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
using System.Collections.Specialized;
using System.Net;

namespace MonoTorrent.TrackerServer
{
    public class ScrapeRequest : TrackerRequest
    {
        static readonly char[] HashSeparators = { ',' };

        /// <summary>
        /// The list of infohashes contained in the scrape request
        /// </summary>
        public IList<InfoHash> InfoHashes { get; }

        /// <summary>
        /// Returns false if any required parameters are missing from the original request.
        /// </summary>
        public override bool IsValid => true;

        public ScrapeRequest (NameValueCollection collection, IPAddress address)
            : base (collection, address)
        {
            InfoHashes = Parameters["info_hash"] is string hashes ? ParseHashes (hashes) : Array.Empty<InfoHash> ();
        }

        static IList<InfoHash> ParseHashes (string infoHash)
        {
            if (string.IsNullOrEmpty (infoHash))
                return Array.Empty<InfoHash> ();

            string[] split = infoHash.Split (HashSeparators, StringSplitOptions.RemoveEmptyEntries);
            var result = new InfoHash[split.Length];
            for (int i = 0; i < split.Length; i++)
                result[i] = InfoHash.UrlDecode (split[i]);
            return result;
        }
    }
}
