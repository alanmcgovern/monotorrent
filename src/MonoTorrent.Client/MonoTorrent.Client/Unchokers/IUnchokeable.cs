using System;
using System.Collections.Generic;

namespace MonoTorrent.Client
{
    interface IUnchokeable
    {
        /// <summary>
        /// Raised whenever the torrent manager's state changes.
        /// </summary>
        event EventHandler<TorrentStateChangedEventArgs> StateChanged;

        /// <summary>
        /// True if we are currently seeding.
        /// </summary>
        bool Seeding { get; }

        /// <summary>
        /// Download speed in bytes/second.
        /// </summary>
        long DownloadSpeed { get; }

        /// <summary>
        /// Upload speed in bytes/second.
        /// </summary>
        long UploadSpeed { get; }

        /// <summary>
        /// Maximum download speed in bytes/second.
        /// </summary>
        long MaximumDownloadSpeed { get; }

        /// <summary>
        /// Maximum upload speed in bytes/second
        /// </summary>
        long MaximumUploadSpeed { get; }

        /// <summary>
        /// The maximum number of peers which can be unchoked concurrently. 0 means unlimited.
        /// </summary>
        int UploadSlots { get; }

        /// <summary>
        /// The number of peers which are currently unchoked.
        /// </summary>
        int UploadingTo { get; set; }

        /// <summary>
        /// List of peers which can be choked/unchoked
        /// </summary>
        List<PeerId> Peers { get; }
    }
}