using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Ssdp.Internal;
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    public delegate void MainLoopResult(object result);

    public delegate object MainLoopJob();

    public delegate void MainLoopTask();

    public delegate bool TimeoutTask();

    public class MainLoop
    {
        private readonly ICache<DelegateTask> cache = new Cache<DelegateTask>(true).Synchronize();

        private readonly TimeoutDispatcher dispatcher = new TimeoutDispatcher();
        private readonly AutoResetEvent handle = new AutoResetEvent(false);
        private readonly Queue<DelegateTask> tasks = new Queue<DelegateTask>();
        internal Thread thread;

        public MainLoop(string name)
        {
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Start();
        }

        private void Loop()
        {
            while (true)
            {
                DelegateTask task = null;

                lock (tasks)
                {
                    if (tasks.Count > 0)
                        task = tasks.Dequeue();
                }

                if (task == null)
                {
                    handle.WaitOne();
                }
                else
                {
                    var reuse = !task.IsBlocking;
                    task.Execute();
                    if (reuse)
                        cache.Enqueue(task);
                }
            }
        }

        private void Queue(DelegateTask task)
        {
            Queue(task, Priority.Normal);
        }

        private void Queue(DelegateTask task, Priority priority)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
                handle.Set();
            }
        }

        public void Queue(MainLoopTask task)
        {
            var dTask = cache.Dequeue();
            dTask.Task = task;
            Queue(dTask);
        }

        public void QueueWait(MainLoopTask task)
        {
            var dTask = cache.Dequeue();
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
            var dTask = cache.Dequeue();
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

            t.WaitHandle.WaitOne();

            if (t.StoredException != null)
                throw new TorrentException("Exception in mainloop", t.StoredException);
        }

        public uint QueueTimeout(TimeSpan span, TimeoutTask task)
        {
            var dTask = cache.Dequeue();
            dTask.Timeout = task;

            return dispatcher.Add(span, delegate
            {
                QueueWait(dTask);
                return dTask.TimeoutResult;
            });
        }

        public AsyncCallback Wrap(AsyncCallback callback)
        {
            return delegate(IAsyncResult result) { Queue(delegate { callback(result); }); };
        }

        private class DelegateTask : ICacheable
        {
            public DelegateTask()
            {
                WaitHandle = new ManualResetEvent(false);
            }

            public bool IsBlocking { get; set; }

            public MainLoopJob Job { get; set; }

            public Exception StoredException { get; set; }

            public MainLoopTask Task { get; set; }

            public TimeoutTask Timeout { get; set; }

            public object JobResult { get; private set; }

            public bool TimeoutResult { get; private set; }

            public ManualResetEvent WaitHandle { get; }

            public void Initialise()
            {
                IsBlocking = false;
                Job = null;
                JobResult = null;
                StoredException = null;
                Task = null;
                Timeout = null;
                TimeoutResult = false;
            }

            public void Execute()
            {
                try
                {
                    if (Job != null)
                        JobResult = Job();
                    else if (Task != null)
                        Task();
                    else if (Timeout != null)
                        TimeoutResult = Timeout();
                }
                catch (Exception ex)
                {
                    StoredException = ex;

                    // FIXME: I assume this case can't happen. The only user interaction
                    // with the mainloop is with blocking tasks. Internally it's a big bug
                    // if i allow an exception to propagate to the mainloop.
                    if (!IsBlocking)
                        throw;
                }
                finally
                {
                    WaitHandle.Set();
                }
            }
        }
    }
}