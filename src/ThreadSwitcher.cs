//
// ThreadSwitcher.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MonoTorrent
{
    internal struct ThreadSwitcher : INotifyCompletion
    {
        static readonly WaitCallback Callback = (state) => ((Action) state!).Invoke ();

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ThreadSwitcher GetAwaiter()
        {
            return this;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsCompleted => false;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void GetResult()
        {

        }


        [EditorBrowsable (EditorBrowsableState.Never)]
        public void OnCompleted (Action continuation)
        {
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            ThreadPool.UnsafeQueueUserWorkItem (ThreadSwitcherWorkItem.GetOrCreate (continuation), false);
#else
            ThreadPool.UnsafeQueueUserWorkItem (Callback, continuation);
#endif
        }

#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER

        internal class ThreadSwitcherWorkItem : IThreadPoolWorkItem
        {
            static readonly Action EmptyAction = () => { };
            static readonly SpinLocked<Stack<ThreadSwitcherWorkItem>> Cache = SpinLocked.Create (new Stack<ThreadSwitcherWorkItem> ());

            public Action Continuation { get; private set; } = EmptyAction;

            public static ThreadSwitcherWorkItem GetOrCreate (Action action)
            {
                using (Cache.Enter (out var cache)) {
                    if (cache.Count == 0) {
                        return new ThreadSwitcherWorkItem { Continuation = action };
                    } else {
                        var worker = cache.Pop ();
                        worker.Continuation = action;
                        return worker;
                    }
                }
            }

            public void Execute ()
            {
                var continuation = Continuation;
                Continuation = EmptyAction;

                using (Cache.Enter (out var cache))
                    cache.Push (this);
                continuation ();
            }
        }
#endif
    }
}
