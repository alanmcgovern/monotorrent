namespace MonoTorrent.Tracker
{
    /// <summary>
    ///     The tracker monitors peers for any ITrackable item
    /// </summary>
    public interface ITrackable
    {
        /// <summary>
        ///     The infohash of the torrent being tracked
        /// </summary>
        InfoHash InfoHash { get; }

        /// <summary>
        ///     The name of the torrent being tracked
        /// </summary>
        string Name { get; }
    }
}