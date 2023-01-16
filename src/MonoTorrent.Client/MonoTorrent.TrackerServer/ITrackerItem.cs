//
// ITrackerItem.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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


using System.Collections.Generic;
using System.Net.Sockets;

namespace MonoTorrent.TrackerServer
{
    /// <summary>
    /// Represents the metadata for a torrent when it is being tracked in a <see cref="TrackerServer"/>
    /// </summary>
    public interface ITrackerItem
    {
        /// <summary>
        /// The number of active seeders
        /// </summary>
        int Complete { get; }

        /// <summary>
        /// The total number of peers being tracked
        /// </summary>
        int Count { get; }

        /// <summary>
        /// The total number of times the torrent has been fully downloaded
        /// </summary>
        int Downloaded { get; }

        /// <summary>
        /// The number of active leechers
        /// </summary>
        int Incomplete { get; }

        /// <summary>
        /// Represents the InfoHash (or equivalent) of the torrent which is being tracked.
        /// </summary>
        ITrackable Trackable { get; }

        /// <summary>
        /// Return a copy of the list of peers
        /// </summary>
        /// <returns></returns>
        List<Peer> GetPeers (AddressFamily addressFamily);
    }
}
