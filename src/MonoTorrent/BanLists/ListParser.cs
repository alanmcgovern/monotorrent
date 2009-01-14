//
// ListParser.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace MonoTorrent.Client
{
    public class BanListParser
    {
        public IEnumerable<AddressRange> Parse(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            
            string result = null;
            Regex r = new Regex(@"([0-9]{1,3}\.){3,3}[0-9]{1,3}");

            while ((result = reader.ReadLine()) != null)
            {
                MatchCollection collection = r.Matches(result);
                if (collection.Count == 1)
                {
                    AddressRange range = new AddressRange();
                    string[] s = collection[0].Captures[0].Value.Split('.');
                    range.Start = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) | (int.Parse(s[3]));
                    range.End = range.Start;
                    yield return range;
                }
                else if (collection.Count == 2)
                {
                    string[] s = collection[0].Captures[0].Value.Split('.');
                    int start = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) | (int.Parse(s[3]));

                    s = collection[1].Captures[0].Value.Split('.');
                    int end = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) | (int.Parse(s[3]));

                    AddressRange range = new AddressRange();
                    range.Start = start;
                    range.End = end;
                    yield return range;
                }
            }
            yield break;
        }
    }
}
