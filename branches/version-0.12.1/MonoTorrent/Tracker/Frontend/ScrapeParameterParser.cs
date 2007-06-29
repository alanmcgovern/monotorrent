//
// ScrapeParameterParser.cs
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
using System.Web;
using System.Collections.Specialized;

namespace MonoTorrent.Tracker 
{
    ///<summary>Class to parse Scrape parameters</summary>
    public class ScrapeParameterParser : ParameterParser
    {
        public ScrapeParameterParser(string rawUrl) : base(rawUrl)
        {
        }
        
        public ScrapeParameters GetParameters()
        {           
            string infoHash = GetConvertedQuery()["info_hash"];
            
            return new ScrapeParameters(GetHashs(infoHash));
        }
        
        private byte[][] GetHashs(string infoHash)
        {
            byte[][] hashs;
            
            if (infoHash == null) {
                return new byte[0][];
            }
            
            if (infoHash.IndexOf(',') > 0) {
                string[] stringHashs = infoHash.Split(',');
                hashs = new byte[stringHashs.Length][];
                for (int i = 0; i < stringHashs.Length; i++) {
                    hashs[i] = HttpUtility.UrlDecodeToBytes(stringHashs[i]);
                }
            } else {
                hashs = new byte[1][];
                hashs[0] = HttpUtility.UrlDecodeToBytes(infoHash);
            }
            return hashs;
        }
    }
    
}
