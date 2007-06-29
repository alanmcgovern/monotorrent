//
// StaticIntervalAlgorithm.cs
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
    ///<summary>this class implements the IIntervalAlgorithm. It has a static
    ///behaivour which means that it just returns the default values or the one
    ///set later. The values are taken from the original BitTorrent tracker.
    ///</summary>
    public class StaticIntervalAlgorithm : IIntervalAlgorithm
    {
        
        public StaticIntervalAlgorithm()
        {
            
        }
        
        public void PeerAdded()
        {
            //ignore
        }
        
        public void PeerRemoved()
        {
            //ignore
        }
        
        ///<summary>Default value is two minutes
        ///</summary>
        public uint MinInterval {
            get {
                return min_interval;
            }
        }
        private uint min_interval = 5*60; //= 2*60;
        
        ///<summary>Default value is 5 minutes
        ///</summary>
        public uint Interval {
            get {
                return interval;
            }
        }
        private uint interval = 1*60; //= 5*60;
                
        public long PeerTimeout {
            get {
                return timeout;
            }
        }
        private long timeout = 45*60*1000;
    }
    
}
