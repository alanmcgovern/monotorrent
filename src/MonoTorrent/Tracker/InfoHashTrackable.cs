using System;
using System.Collections.Generic;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class InfoHashTrackable : ITrackable
    {
        private InfoHash infoHash;
        private string name;

        public InfoHashTrackable(Torrent torrent)
        {
            Check.Torrent(torrent);

            name = torrent.Name;
            infoHash = torrent.InfoHash;
        }

        public InfoHashTrackable(string name, InfoHash infoHash)
        {
            Check.InfoHash(infoHash);

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name cannot be null or empty", "name");

            this.infoHash = infoHash;
            this.name = name;
        }

        public InfoHash InfoHash
        {
            get { return infoHash; }
        }

        public string Name
        {
            get { return name; }
        }
    }
}
