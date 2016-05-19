namespace MonoTorrent.Client
{
    internal interface IRateLimiter
    {
        bool Unlimited { get; }
        bool TryProcess(int amount);
        void UpdateChunks(int maxRate, int actualRate);
    }
}