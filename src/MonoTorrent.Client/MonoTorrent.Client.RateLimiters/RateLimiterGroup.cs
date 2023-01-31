//
// RateLimiterGroup.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Client.RateLimiters
{
    sealed class RateLimiterGroup : IRateLimiter, IEnumerable<IRateLimiter>
    {
        readonly List<IRateLimiter> limiters;

        public bool Unlimited {
            get {
                for (int i = 0; i < limiters.Count; i++)
                    if (!limiters[i].Unlimited)
                        return false;
                return true;
            }
        }

        public RateLimiterGroup ()
        {
            limiters = new List<IRateLimiter> ();
        }

        public void Add (IRateLimiter limiter)
        {
            Check.Limiter (limiter);
            limiters.Add (limiter);
        }

        public void Remove (IRateLimiter limiter)
        {
            Check.Limiter (limiter);
            limiters.Remove (limiter);
        }

        public bool TryProcess (long amount)
        {
            for (int i = 0; i < limiters.Count; i++) {
                if (limiters[i].Unlimited)
                    continue;
                else if (!limiters[i].TryProcess (amount))
                    return false;
            }
            return true;
        }

        public IEnumerator<IRateLimiter> GetEnumerator ()
        {
            return limiters.GetEnumerator ();
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return limiters.GetEnumerator ();
        }
    }
}
