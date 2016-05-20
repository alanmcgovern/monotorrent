using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class PauseLimiter : IRateLimiter
    {
        private readonly TorrentManager manager;

        public PauseLimiter(TorrentManager manager)
        {
            this.manager = manager;
        }

        public bool Unlimited
        {
            get { return manager.State != TorrentState.Paused; }
        }

        public bool TryProcess(int amount)
        {
            return Unlimited;
        }

        public void UpdateChunks(int maxRate, int actualRate)
        {
            // This is a simple on/off limiter
        }
    }
}