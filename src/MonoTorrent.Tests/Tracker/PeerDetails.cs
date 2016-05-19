using System.Net;
using MonoTorrent.Tracker;

namespace MonoTorrent.Tests.Tracker
{
    public class PeerDetails
    {
        public IPAddress ClientAddress;
        public long Downloaded;
        public string peerId;
        public int Port;
        public long Remaining;
        public ITrackable trackable;
        public long Uploaded;
    }
}