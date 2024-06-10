//
// SemaphoreSlimExtensions.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using System.Threading;

using ReusableTasks;

namespace MonoTorrent
{
    static class SemaphoreSlimExtensions
    {
        internal readonly struct Releaser : IDisposable
        {
            readonly SemaphoreSlim? Semaphore;

            public Releaser (SemaphoreSlim semaphore)
                => Semaphore = semaphore;

            public void Dispose ()
                => Semaphore?.Release ();
        }

        internal static async ReusableTask<Releaser> EnterAsync (this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync ().ConfigureAwait (false);
            return new Releaser (semaphore);
        }
    }

    class ReusableSemaphore
    {
        static readonly Queue<ReusableTaskCompletionSource<object?>> Cache = new Queue<ReusableTaskCompletionSource<object?>> ();

        public readonly struct Releaser : IDisposable
        {
            readonly ReusableSemaphore Owner { get; }

            internal Releaser (ReusableSemaphore owner)
                => Owner = owner;

            public void Dispose ()
                => Owner?.ReleaseOne ();
        }

        int activeCount;
        Queue<ReusableTaskCompletionSource<object?>> nextWaiter;

        /// <summary>
        /// The maximum concurrency. A value of 0 means unlimited.
        /// </summary>
        public int Count { get; private set; }

        public ReusableSemaphore (int count)
        {
            Count = count;
            nextWaiter = new Queue<ReusableTaskCompletionSource<object?>> ();
        }

        public async ReusableTask<Releaser> EnterAsync ()
        {
            ReusableTaskCompletionSource<object?> task;
            lock (Cache) {
                ++activeCount;
                if (activeCount <= Count || Count == 0)
                    return new Releaser (this);

                task = Cache.Count > 0 ? Cache.Dequeue () : new ReusableTaskCompletionSource<object?> ();
                nextWaiter.Enqueue (task);
            }
            await task.Task.ConfigureAwait (false);
            lock (Cache)
                Cache.Enqueue (task);
            return new Releaser (this);
        }

        public bool TryEnter (out Releaser value)
        {
            lock (Cache) {
                if (activeCount < Count) {
                    ++activeCount;
                    value = new Releaser (this);
                    return true;
                } else {
                    value = default;
                    return false;
                }
            }
        }

        public void ChangeCount (int newCount)
        {
            if (newCount < 0)
                throw new ArgumentOutOfRangeException (nameof (newCount));

            lock (Cache) {
                // If we have increased the number of slots, kick off the required number of pending tasks
                // to consume the new slots.
                if (newCount == 0 || newCount > Count) {
                    var delta = newCount - Count;
                    while ((newCount == 0 || delta-- > 0) && nextWaiter.Count > 0)
                        nextWaiter.Dequeue ().SetResult (null);
                }
                Count = newCount;
            }
        }

        void ReleaseOne ()
        {
            lock (Cache) {
                --activeCount;

                // If the semaphore has recently decreased the number of slots, we *may* not want to
                // start one of the queued tasks yet. We need to wait for concurrency to reduce.
                // If we have a count of '0' then we'll never enqueue waiters as it's treated as 'unlimited'
                if (nextWaiter.Count > 0 && (activeCount - nextWaiter.Count) < Count)
                    nextWaiter.Dequeue ().SetResult (null);
            }
        }
    }
}
