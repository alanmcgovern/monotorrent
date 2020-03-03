//
// Enums.cs
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


namespace MonoTorrent.Dht
{
    public enum DhtState
    {
        NotReady,
        Initialising,
        Ready
    }
}

namespace MonoTorrent.Client
{
    public enum TorrentState
    {
        Stopped,
        Paused,
        Starting,
        Downloading,
        Seeding,
        Hashing,
        HashingPaused,
        Stopping,
        Error,
        Metadata
    }
}

namespace MonoTorrent.Client.Tracker
{
    public enum TrackerState
    {
        /// <summary>
        /// A request has not been sent yet.
        /// </summary>
        Unknown,
        /// <summary>
        /// Currently sending a request.
        /// </summary>
        Connecting,
        /// <summary>
        /// The most recent request completed successfully.
        /// </summary>
        Ok,
        /// <summary>
        /// The tracker was unreachable/offline.
        /// </summary>
        Offline,
        /// <summary>
        /// The tracker was reachable but the response it sent was invalid.
        /// </summary>
        InvalidResponse
    }
}

namespace MonoTorrent
{
    public enum ListenerStatus
    {
        Listening,
        PortNotFree,
        NotListening
    }

    public enum Direction
    {
        None,
        Incoming,
        Outgoing
    }

    public enum Priority
    {
        DoNotDownload = 0,
        Lowest = 1,
        Low = 2,
        Normal = 4,
        High = 8,
        Highest = 16,
        Immediate = 32
    }

    public enum TorrentEvent
    {
        None,
        Started,
        Stopped,
        Completed
    }

    enum PeerListType
    {
        NascentPeers,
        CandidatePeers,
        OptimisticUnchokeCandidatePeers
    }
}
