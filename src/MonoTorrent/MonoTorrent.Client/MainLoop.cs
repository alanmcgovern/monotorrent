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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Mono.Ssdp.Internal;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
	public delegate void MainLoopResult (object result);
    public delegate object MainLoopJob();
    public delegate void MainLoopTask();
    public delegate bool TimeoutTask();

    public class MainLoop
    {
        private class DelegateTask : ICacheable
        {
            private ManualResetEventSlim handle;
            private bool isBlocking;
            private MainLoopJob job;
            private object jobResult;
            private Exception storedException;
            private MainLoopTask task;
            private TimeoutTask timeout;
            private bool timeoutResult;

            public bool IsBlocking
            {
                get { return isBlocking; }
                set { isBlocking = value; }
            }

            public MainLoopJob Job
            {
                get { return job; }
                set { job = value; }
            }

            public Exception StoredException
            {
                get { return storedException; }
                set { storedException = value; }
            }

            public MainLoopTask Task
            {
                get { return task; }
                set { task = value; }
            }

            public TimeoutTask Timeout
            {
                get { return timeout; }
                set { timeout = value; }
            }

            public object JobResult
            {
                get { return jobResult; }
            }

            public bool TimeoutResult
            {
                get { return timeoutResult; }
            }

            public ManualResetEventSlim WaitHandle
            {
                get { return handle??(handle=new ManualResetEventSlim()); }
            }

            public DelegateTask()
            {
            }
            
            public void Execute()
            {
                try
                {
                    if (job != null)
                        jobResult = job();
                    else if (task != null)
                        task();
                    else if (timeout != null)
                        timeoutResult = timeout();
                }
                catch (Exception ex)
                {
                    storedException = ex;

                    // FIXME: I assume this case can't happen. The only user interaction
                    // with the mainloop is with blocking tasks. Internally it's a big bug
                    // if i allow an exception to propagate to the mainloop.
                    if (!IsBlocking)
                        throw;
                }
                finally
                {
                    if(handle!=null)
                        handle.Set();
                }
            }

            public void Initialise()
            {
                isBlocking = false;
                job = null;
                jobResult = null;
                storedException = null;
                task = null;
                timeout = null;
                timeoutResult = false;
                handle = null;
            }
        }

        TimeoutDispatcher dispatcher = new TimeoutDispatcher();
        ICache<DelegateTask> cache = new Cache<DelegateTask>(true).Synchronize();
        BlockingCollection<DelegateTask> tasks = new BlockingCollection<DelegateTask>();
        internal Thread thread;

        public MainLoop(string name)
        {
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Start();
        }

        void Loop()
        {
            while (true)
            {
                DelegateTask task = tasks.Take();
                bool reuse = !task.IsBlocking;
                task.Execute();
                if (reuse)
                    cache.Enqueue(task);
            }
        }

        private void Queue(DelegateTask task)
        {
            Queue(task, Priority.Normal);
        }

        private void Queue(DelegateTask task, Priority priority)
        {
            tasks.Add(task);
        }

        public void Queue(MainLoopTask task)
        {
            DelegateTask dTask = cache.Dequeue();
            dTask.Task = task;
            Queue(dTask);
        }

        public void QueueWait(MainLoopTask task)
        {
            DelegateTask dTask = cache.Dequeue();
            dTask.Task = task;
            try
            {
                QueueWait(dTask);
            }
            finally
            {
                cache.Enqueue(dTask);
            }
        }

        public object QueueWait(MainLoopJob task)
        {
            DelegateTask dTask = cache.Dequeue();
            dTask.Job = task;

            try
            {
                QueueWait(dTask);
                return dTask.JobResult;
            }
            finally
            {
                cache.Enqueue(dTask);
            }
        }

        private void QueueWait(DelegateTask t)
        {
            t.WaitHandle.Reset();
            t.IsBlocking = true;
            if (Thread.CurrentThread == thread)
                t.Execute();
            else
                Queue(t, Priority.Highest);

            t.WaitHandle.Wait();

            if (t.StoredException != null)
                throw new TorrentException("Exception in mainloop", t.StoredException);
        }

        public long QueueTimeout(TimeSpan span, TimeoutTask task)
        {
            DelegateTask dTask = cache.Dequeue();
            dTask.Timeout = task;

            return dispatcher.Add(span, delegate {
                QueueWait(dTask);
                return dTask.TimeoutResult;
            });
        }

        public AsyncCallback Wrap(AsyncCallback callback)
        {
            return delegate(IAsyncResult result) {
                Queue(delegate {
                    callback(result);
                });
            };
        }
    }
}