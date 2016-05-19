using MonoTorrent.Client.Connections;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal static partial class NetworkIO
    {
        private class AsyncIOState : ICacheable
        {
            public byte[] Buffer { get; private set; }

            public AsyncIOCallback Callback { get; private set; }

            public IConnection Connection { get; private set; }

            public int Count { get; private set; }

            public int Offset { get; set; }

            public ConnectionMonitor ManagerMonitor { get; private set; }

            public ConnectionMonitor PeerMonitor { get; private set; }

            public IRateLimiter RateLimiter { get; private set; }

            public int Remaining { get; set; }

            public object State { get; set; }

            public TransferType TransferType
            {
                get { return Count >= Piece.BlockSize ? TransferType.Data : TransferType.Protocol; }
            }

            public void Initialise()
            {
                Initialise(null, null, 0, 0, null, null, null, null, null);
            }

            public AsyncIOState Initialise(IConnection connection, byte[] buffer, int offset, int count,
                AsyncIOCallback callback,
                object state, IRateLimiter limiter,
                ConnectionMonitor peerMonitor, ConnectionMonitor managerMonitor)
            {
                Connection = connection;
                Buffer = buffer;
                Count = count;
                Callback = callback;
                Offset = offset;
                ManagerMonitor = managerMonitor;
                PeerMonitor = peerMonitor;
                RateLimiter = limiter;
                Remaining = count;
                State = state;
                return this;
            }
        }
    }
}