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



using System;

namespace MonoTorrent.Common
{
    public enum PeerStatus
    {
        Available,
        Connecting,
        Connected
    }

    public enum Direction
    {
        None,
        Incoming,
        Outgoing
    }

    public enum TorrentState
    {
        Stopped,
        Paused,
        Downloading,
        Seeding,
        Hashing,
        Stopping
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

    public enum TrackerState
    {
        Unknown,
        Announcing,
        AnnouncingFailed,
        AnnounceSuccessful,
        Scraping,
        ScrapingFailed,
        ScrapeSuccessful
    }

    public enum TorrentEvent
    {
        None,
        Started,
        Stopped,
        Completed
    }

    public enum PeerConnectionEvent
    {
        IncomingConnectionReceived,
        OutgoingConnectionCreated,
        Disconnected
    }

    public enum PieceEvent
    {
        BlockWriteQueued,
        BlockNotRequested,
        BlockWrittenToDisk,
        HashPassed,
        HashFailed
    }

	public enum PeerListType
	{
		NascentPeers,
		CandidatePeers,
		OptimisticUnchokeCandidatePeers
	}
}
