//
// FastResumeMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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


namespace MonoTorrent.Client
{
    public enum FastResumeMode
    {
        /// <summary>
        /// When <see cref="EngineSettings.AutoSaveLoadFastResume"/> is enabled the engine will delete fast resume data from disk when
        /// the <see cref="TorrentManager"/> enters the <see cref="TorrentState.Downloading"/> state, or if the hash check is cancelled.
        /// FastResume data will be written to disk when the <see cref="TorrentManager"/> enters <see cref="TorrentState.Seeding"/> mode,
        /// or when the torrent enters the <see cref="TorrentState.Stopped"/> state and no errors occurred. If a crash occurs, a full
        /// hash check will be performed the next time the torrent is started, meaning there is no chance duplicate data will be downloaded.
        /// </summary>
        Accurate,
        /// <summary>
        /// When <see cref="EngineSettings.AutoSaveLoadFastResume"/> is enabled the engine will not delete fast resume data from disk when
        /// the <see cref="TorrentManager"/> enters the <see cref="TorrentState.Downloading"/> state. In this mode the engine will write
        /// an updated copy of the fast resume data on a regular cadence. In the event of a crash, the most recent fast resume data will
        /// be loaded from disk and a full hash check will not be performed. This may result in a small amount of data being redownloaded.
        /// </summary>
        BestEffort,
    }
}