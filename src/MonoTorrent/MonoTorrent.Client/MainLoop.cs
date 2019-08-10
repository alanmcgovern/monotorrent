//
// MainLoop.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Client
{
    public class MainLoop : SynchronizationContext, INotifyCompletion
    {
        static readonly ICache<CacheableManualResetEventSlim> cache = new Cache<CacheableManualResetEventSlim> (true).Synchronize ();

        struct QueuedTask
        {
            public Action Action;

            public AsyncCallback Callback;
            public IAsyncResult CallbackResult;

            public SendOrPostCallback SendOrPostCallback;
            public object State;

            public ManualResetEventSlim WaitHandle;
        }

        class CacheableManualResetEventSlim : ManualResetEventSlim, ICacheable
        {
            public void Initialise() => Reset();
        }

        readonly Queue<QueuedTask> actions = new Queue<QueuedTask> ();
        readonly ManualResetEventSlim actionsWaiter = new ManualResetEventSlim ();
        readonly Thread thread;

        public MainLoop(string name)
        {
            thread = new Thread(Loop) {
                Name = name,
                IsBackground = true
            };
            thread.Start();
        }

        void Loop()
        {
            SetSynchronizationContext (this);
            using (ExecutionContext.SuppressFlow())
                while (true)
                {
                    QueuedTask? task = null;

                    lock (actions)
                    {
                        if (actions.Count > 0)
                            task = actions.Dequeue();
                        else
                            actionsWaiter.Reset();
                    }

                    if (!task.HasValue)
                    {
                        actionsWaiter.Wait();
                    }
                    else
                    {
                        try
                        {
                            task.Value.Action?.Invoke();
                            task.Value.Callback?.Invoke(task.Value.CallbackResult);
                            task.Value.SendOrPostCallback?.Invoke(task.Value.State);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Unexpected main loop exception: {0}", ex);
                        }
                        finally
                        {
                            task.Value.WaitHandle?.Set();
                        }
                    }
                }
        }

        public void Queue(Action action)
        {
            Queue (new QueuedTask { Action = action });
        }

        public void QueueWait(Action action)
        {
            Send(t => action (), null);
        }

        public object QueueWait(Func<object> func)
        {
            object result = null;
            Send(t => result = func(), null);
            return result;
        }

        public void QueueTimeout(TimeSpan span, Func<bool> task)
        {
            if (span.TotalMilliseconds < 1)
                span = TimeSpan.FromMilliseconds(1);
            bool disposed = false;
            Timer timer = null;
            SendOrPostCallback callback = state => {
                if (!disposed && !task()) {
                    disposed = true;
                    timer.Dispose();
                }
            };

            timer = new Timer(state => {
                Post(callback, null);
            }, null, span, span);
        }

        void Queue (QueuedTask task)
        {
            lock (actions) {
                actions.Enqueue (task);
                if (actions.Count == 1)
                    actionsWaiter.Set ();
            }
        }

        public AsyncCallback Wrap(AsyncCallback callback)
        {
            return delegate(IAsyncResult result) {
                Queue (new QueuedTask { Callback = callback, CallbackResult = result });
            };
        }

        [EditorBrowsable (EditorBrowsableState.Never)]
        public override void Post(SendOrPostCallback d, object state)
        {
            Queue (new QueuedTask { SendOrPostCallback = d, State = state });
        }

        [EditorBrowsable (EditorBrowsableState.Never)]
        public override void Send(SendOrPostCallback d, object state)
        {
            if (thread == Thread.CurrentThread)
            {
                d(state);
            }
            else
            {
                var waiter = cache.Dequeue();
                Queue (new QueuedTask { SendOrPostCallback = d, State = state, WaitHandle = waiter });
                waiter.Wait();
                cache.Enqueue(waiter);
            }
        }

        #region If you await the MainLoop you'll swap to it's thread!
        [EditorBrowsable (EditorBrowsableState.Never)]
        public MainLoop GetAwaiter () => this;

        [EditorBrowsable (EditorBrowsableState.Never)]
        public bool IsCompleted => thread == Thread.CurrentThread;

        [EditorBrowsable (EditorBrowsableState.Never)]
        public void GetResult()
        {

        }

        [EditorBrowsable (EditorBrowsableState.Never)]
        public void OnCompleted(Action continuation)
        {
            Queue (new QueuedTask { Action = continuation });
        }
        #endregion
    }
}