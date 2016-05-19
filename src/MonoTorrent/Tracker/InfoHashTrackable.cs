using System;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class InfoHashTrackable : ITrackable
    {
        public InfoHashTrackable(Torrent torrent)
        {
            Check.Torrent(torrent);

            Name = torrent.Name;
            InfoHash = torrent.InfoHash;
        }

        public InfoHashTrackable(string name, InfoHash infoHash)
        {
            Check.InfoHash(infoHash);

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name cannot be null or empty", "name");

            InfoHash = infoHash;
            Name = name;
        }

        public InfoHash InfoHash { get; }

        public string Name { get; }
    }
}