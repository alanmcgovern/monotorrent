namespace MonoTorrent.Client
{
    internal class DiskWriterLimiter : IRateLimiter
    {
        private readonly DiskManager manager;

        public DiskWriterLimiter(DiskManager manager)
        {
            this.manager = manager;
        }

        public bool Unlimited
        {
            get { return manager.QueuedWrites < 20; }
        }

        public bool TryProcess(int amount)
        {
            return Unlimited;
        }

        public void UpdateChunks(int maxRate, int actualRate)
        {
            // This is a simple on/off limiter which prevents
            // additional downloading if the diskwriter is backlogged
        }
    }
}