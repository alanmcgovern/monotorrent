using System.Collections.Generic;

namespace MonoTorrent.Client
{
    internal class RateLimiterGroup : IRateLimiter
    {
        private readonly List<IRateLimiter> limiters;

        public RateLimiterGroup()
        {
            limiters = new List<IRateLimiter>();
        }

        public bool Unlimited
        {
            get
            {
                for (var i = 0; i < limiters.Count; i++)
                    if (!limiters[i].Unlimited)
                        return false;
                return true;
            }
        }

        public bool TryProcess(int amount)
        {
            for (var i = 0; i < limiters.Count; i++)
            {
                if (limiters[i].Unlimited)
                    continue;
                if (!limiters[i].TryProcess(amount))
                    return false;
            }
            return true;
        }

        public void UpdateChunks(int maxRate, int actualRate)
        {
            for (var i = 0; i < limiters.Count; i++)
                limiters[i].UpdateChunks(maxRate, actualRate);
        }

        public void Add(IRateLimiter limiter)
        {
            Check.Limiter(limiter);
            limiters.Add(limiter);
        }

        public void Remove(IRateLimiter limiter)
        {
            Check.Limiter(limiter);
            limiters.Remove(limiter);
        }
    }
}