//
// ScrapeParameters.cs
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
using System.Collections.Specialized;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Net;


namespace MonoTorrent.Tracker
{
    public class ScrapeParameters : RequestParameters
    {
        private List<byte[]> hashs;
        
        public int Count
        {
            get { return hashs.Count; }
        }
        
        public List<byte[]> InfoHashes
        {
            get { return hashs; }
        }
        
        public override bool IsValid
        {
            get { return true; }
        }
        
        public ScrapeParameters(NameValueCollection collection, IPAddress address)
            : base(collection, address)
        {
            hashs = new List<byte[]>();
            ParseHashes(Parameters["info_hash"]);
        }
        
        private void ParseHashes(string infoHash)
        {
            if (string.IsNullOrEmpty(infoHash))
                return;
            
            if (infoHash.IndexOf(',') > 0)
            {
                string[] stringHashs = infoHash.Split(',');
                for (int i = 0; i < stringHashs.Length; i++)
                    hashs.Add(HttpUtility.UrlDecodeToBytes(stringHashs[i]));
            }
            else
            {
                hashs.Add(HttpUtility.UrlDecodeToBytes(infoHash));
            }
        }

        public IEnumerator GetEnumerator()
        {
            return hashs.GetEnumerator();
        }
    }
}
