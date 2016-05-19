using System;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    internal class NullDhtEngine : IDhtEngine
    {
        public event EventHandler<PeersFoundEventArgs> PeersFound;
        public event EventHandler StateChanged;

        public bool Disposed
        {
            get { return false; }
        }

        public DhtState State
        {
            get { return DhtState.NotReady; }
        }

        public void Add(BEncodedList nodes)
        {
        }

        public void Announce(InfoHash infohash, int port)
        {
        }

        public void Dispose()
        {
        }

        public void GetPeers(InfoHash infohash)
        {
        }

        public byte[] SaveNodes()
        {
            return new byte[0];
        }

        public void Start()
        {
        }

        public void Start(byte[] initialNodes)
        {
        }

        public void Stop()
        {
        }
    }
}