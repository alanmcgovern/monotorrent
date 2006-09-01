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


namespace MonoTorrent.Tracker
{
    ///<summary>Struct to hold and iterate parameters used by the Scraperequest.</summary> 
    public struct ScrapeParameters
    {
        byte[][] hashs;
        
        ///<summary>Number of infoHashs.</summary>
        public int Count
        {
            get { return hashs.Length; }
        }
        
        ///<summary>The first infohash</summary>
        public byte[] InfoHash
        {
            get { return hashs[0]; }
        }
        
        public ScrapeParameters(byte[][] infoHashs)
        {
            hashs = infoHashs;    
        }
        
        ///<summary>Enumerate over the infohashs</summary>
        public System.Collections.IEnumerator GetEnumerator()
        {
            for (int i = 0; i < hashs.Length; i++)
            {
                yield return hashs[i];
            }
        }
    }
    
}
