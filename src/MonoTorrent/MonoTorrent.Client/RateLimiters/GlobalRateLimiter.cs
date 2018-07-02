using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class GlobalRateLimiter : IRateLimiter
    {
        public GlobalRateLimiter(IRateLimiter limiter)
        {
            Check.Limiter(limiter);
            this.limiter = limiter;
        }

        IRateLimiter limiter;

        public bool TryProcess(int amount)
        {
            return limiter.TryProcess(amount);
        }

        public bool Unlimited
        {
            get { return limiter.Unlimited; }
        }

        public void UpdateChunks(int maxRate, int actualRate)
        {
            // Don't UpdateChunks on the wrapped limiter. The engine calls it directly.
        }
    }
}
