//
// RateLimiter.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    public class RateLimiter
    {
        #region Member Variables

        private int savedError;
        public int Chunks;

        #endregion


        #region Methods

        public void UpdateChunks(int maxRate, int actualRate)
        {
            // From experimentation, i found that increasing by 5% gives more accuate rate limiting
            // for peer communications. For disk access and whatnot, a 5% overshoot is fine.
            maxRate = (int)(maxRate * 1.05);
            int errorRateDown = maxRate - actualRate;
            int delta = (int)(0.4 * errorRateDown + 0.6 * this.savedError);
            this.savedError = errorRateDown;


            int increaseAmount = (int)((maxRate + delta) / ConnectionManager.ChunkLength);
            Interlocked.Add(ref this.Chunks, increaseAmount);
            if (this.Chunks > (maxRate * 1.2 / ConnectionManager.ChunkLength))
                Interlocked.Exchange(ref this.Chunks, (int)(maxRate * 1.2 / ConnectionManager.ChunkLength));

            if (this.Chunks < (maxRate / ConnectionManager.ChunkLength / 2))
                Interlocked.Exchange(ref this.Chunks, (maxRate / ConnectionManager.ChunkLength / 2));

            if (maxRate == 0)
                Chunks = 0;
        }

        #endregion
    }
}
