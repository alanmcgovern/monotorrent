//
// TorrentStateChangedEventArgs.cs
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
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides the data needed to handle a TorrentStateChanged event
    /// </summary>
    public class TorrentStateChangedEventArgs : TorrentEventArgs
    {
        #region Member Variables
        /// <summary>
        /// The old state for the torrent
        /// </summary>
        public TorrentState OldState
        {
            get { return this.oldState; }
        }
        private TorrentState oldState;


        /// <summary>
        /// The new state for the torrent
        /// </summary>
        public TorrentState NewState
        {
            get { return this.newState; }
        }
        private TorrentState newState;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new TorrentStateChangedEventArgs
        /// </summary>
        /// <param name="oldState">The old state of the Torrent</param>
        /// <param name="newState">The new state of the Torrent</param>
        public TorrentStateChangedEventArgs(TorrentManager manager, TorrentState oldState, TorrentState newState)
            : base(manager)
        {
            this.oldState = oldState;
            this.newState = newState;
        }
        #endregion
    }
}