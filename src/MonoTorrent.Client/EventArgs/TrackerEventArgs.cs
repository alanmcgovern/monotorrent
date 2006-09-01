//
// TrackerEventArgs.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides the data needed to handle a TrackerUpdate event
    /// </summary>
    public class TrackerUpdateEventArgs : EventArgs
    {
        #region Member Variables
        /// <summary>
        /// The current status of the tracker update
        /// </summary>
        public TrackerState State
        {
            get { return this.state; }
        }
        private TrackerState state;

        /// <summary>
        /// The response (if any) of the tracker
        /// </summary>
        public byte[] Response
        {
            get { return this.response; }
        }
        private byte[] response;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TrackerUpdateEventArgs
        /// </summary>
        /// <param name="state">The current state of the update</param>
        /// <param name="response">The response of the tracker (if any)</param>
        public TrackerUpdateEventArgs(TrackerState state, byte[] response)
        {
            this.state = state;
            this.response = response;
        }
        #endregion
    }
}
