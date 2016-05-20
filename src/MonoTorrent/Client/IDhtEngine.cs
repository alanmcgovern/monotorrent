using System;
using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public interface IDhtEngine : IDisposable
    {
        bool Disposed { get; }
        DhtState State { get; }
        event EventHandler<PeersFoundEventArgs> PeersFound;
        byte[] SaveNodes();
        void Add(BEncodedList nodes);
        void Announce(InfoHash infohash, int port);
        void GetPeers(InfoHash infohash);
        void Start();
        void Start(byte[] initialNodes);
        event EventHandler StateChanged;
        void Stop();
    }
}