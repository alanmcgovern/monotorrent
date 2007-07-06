//
// TorrentWatcher.cs
//
// Authors:
//   Stephane Zanoni   stephane@ethernal.net
//
// Copyright (C) 2006 Stephane Zanoni
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

namespace MonoTorrent.Common
{
    /// <summary>
    /// Main controller class for ITorrentWatcher
    /// </summary>
    public class TorrentWatchers : ITorrentWatcherCollection
    {
        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        public TorrentWatchers()
            : base()
        {

        }

        #endregion


        #region Methods

        /// <summary>
        /// 
        /// </summary>
        public void StartAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].StartWatching();
        }


        /// <summary>
        /// 
        /// </summary>
        public void StopAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].StopWatching();
        }


        /// <summary>
        /// 
        /// </summary>
        public void ForceScanAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].ForceScan();
        }

        #endregion
    }
}
