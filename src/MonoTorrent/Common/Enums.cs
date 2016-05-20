namespace MonoTorrent
{
    public enum DhtState
    {
        NotReady,
        Initialising,
        Ready
    }
}

namespace MonoTorrent.Common
{
    public enum ListenerStatus
    {
        Listening,
        PortNotFree,
        NotListening
    }

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
        Stopping,
        Error,
        Metadata
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
        Ok,
        Offline,
        InvalidResponse
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