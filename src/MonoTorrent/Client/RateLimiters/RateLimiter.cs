using System.Threading;

namespace MonoTorrent.Client
{
    internal class RateLimiter : IRateLimiter
    {
        private int chunks;
        private int savedError;

        public RateLimiter()
        {
            UpdateChunks(0, 0);
        }

        public bool Unlimited { get; private set; }

        public void UpdateChunks(int maxRate, int actualRate)
        {
            Unlimited = maxRate == 0;
            if (Unlimited)
                return;

            // From experimentation, i found that increasing by 5% gives more accuate rate limiting
            // for peer communications. For disk access and whatnot, a 5% overshoot is fine.
            maxRate = (int) (maxRate*1.05);
            var errorRateDown = maxRate - actualRate;
            var delta = (int) (0.4*errorRateDown + 0.6*savedError);
            savedError = errorRateDown;


            var increaseAmount = (maxRate + delta)/ConnectionManager.ChunkLength;
            Interlocked.Add(ref chunks, increaseAmount);
            if (chunks > maxRate*1.2/ConnectionManager.ChunkLength)
                Interlocked.Exchange(ref chunks, (int) (maxRate*1.2/ConnectionManager.ChunkLength));

            if (chunks < maxRate/ConnectionManager.ChunkLength/2)
                Interlocked.Exchange(ref chunks, maxRate/ConnectionManager.ChunkLength/2);

            if (maxRate == 0)
                chunks = 0;
        }

        public bool TryProcess(int amount)
        {
            if (Unlimited)
                return true;

            int c;
            do
            {
                c = chunks;
                if (c < amount)
                    return false;
            } while (Interlocked.CompareExchange(ref chunks, c - amount, c) != c);
            return true;
        }
    }
}