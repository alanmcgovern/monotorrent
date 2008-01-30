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
        private int min_interval = 10 * 60; // 10 minutes
        private int interval = 45 * 60;     // 45 minutes
        private int timeout = 50 * 60;      // 50 minutes

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
        
        ///<summary>
        /// Minimum announce interval in seconds
        ///</summary>
        public int MinInterval 
        {
            get { return min_interval; }
        }
        
        ///<summary>
        /// Regular announce interval in seconds
        ///</summary>
        public int Interval
        {
            get { return interval;  }
        }

        /// <summary>
        /// Peer timeout interval in seconds
        /// </summary>
        public int PeerTimeout
        {
            get {  return timeout; }
        }
    }
}
