//
// ParameterParser.cs
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
using System.Diagnostics;
using System.Collections.Specialized;

namespace MonoTorrent.Tracker
{
    
    public abstract class ParameterParser
    {
        string raw_url;
        public ParameterParser(string rawUrl)
        {
            raw_url = rawUrl;
            if (rawUrl.IndexOf('?') != rawUrl.LastIndexOf('?')) {
                throw new ArgumentException("wrong uri format: more than one ?", rawUrl);
            }
        }
        
        public NameValueCollection GetConvertedQuery()         
        {            
            NameValueCollection c = new NameValueCollection();
            string queryString = raw_url;
            
            if (raw_url.IndexOf('?') >= 0) {
                queryString = queryString.Split('?')[1];
            } else {
                Debug.WriteLine("no parameters");
                return c;
            }
            
            foreach (string equation in queryString.Split('&')) {
                Debug.WriteLine("cgi kvp: " + equation);
                if (equation.IndexOf('=') < 0) {
                    c.Add(equation, null);
                }
                string key = equation.Split('=')[0];
                string val = equation.Split('=')[1];
                c.Add(key, val);
            }
            
            return c;
        }
    }
    
}
