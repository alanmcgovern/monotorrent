//
// IIntervalAlgorithm.cs
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
	///<summary>This interface is used to set
	///</summary>
	public interface IIntervalAlgorithm
	{
        ///<summary>this method is called when a peer was added to the tracker</summary>
        void PeerAdded();
        
        ///<summary>this method is called when a peer was removed from the tracker</summary>
        void PeerRemoved();
        
        ///<summary>Returns the minimum interval</summary> 
        uint MinInterval
        {
            get;
        }
        
        ///<summary>Returns the interval</summary>
        uint Interval
        {
            get;
        }
        
        ///<summary>timeout before peer is removed</summary>
        long PeerTimeout
        {
            get;
        }
	}
}
