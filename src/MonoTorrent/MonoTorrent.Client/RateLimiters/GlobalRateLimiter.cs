using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoTorrent.Client
{
    /// <summary>
    ///  This class fixes issue #33. https://github.com/mono/monotorrent/issues/33
    ///  We want torrent managers to heed the result of the TryAdvice method of the engine's rate limiter, but we
    ///  don't want them calling UpdateChunks on the global limiter because it clobbers the calculation that the 
    ///  engine already did. 
    /// </summary>
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
