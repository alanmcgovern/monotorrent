using MonoTorrent.Tracker;

namespace MonoTorrent.Tests.Tracker
{
    public class Trackable : ITrackable
    {
        public Trackable(InfoHash infoHash, string name)
        {
            InfoHash = infoHash;
            Name = name;
        }

        public InfoHash InfoHash { get; }

        public string Name { get; }
    }
}