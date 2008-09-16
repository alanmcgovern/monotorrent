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
using System.Text;
using System.Threading;
using Mono.Ssdp.Internal;

namespace MonoTorrent.Client
{
    public delegate object MainLoopJob();
    public delegate void MainLoopTask();
    public class MainLoop : IDisposable
    {
        private class DelegateTask
        {
            public ManualResetEvent Handle;
            private object result;
            private MainLoopJob task;

            public object Result
            {
                get { return result; }
            }
            public DelegateTask(MainLoopJob task)
            {
                this.task = task;
            }

            public void Execute()
            {
                result = task();
                if (Handle != null)
                    Handle.Set();
            }
        }

        TimeoutDispatcher dispatcher = new TimeoutDispatcher();
        bool disposed;
        AutoResetEvent handle = new AutoResetEvent(false);
        Queue<DelegateTask> tasks = new Queue<DelegateTask>();
        internal Thread thread;

        public bool Disposed
        {
            get { return disposed; }
        }

        public MainLoop(string name)
        {
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = name;
            thread.Start();
        }

        void Loop()
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
                    if (disposed)
                        return;

                    handle.WaitOne();
                }
                else
                {
                    task.Execute();
                }
            }
        }

        private void Queue(DelegateTask task)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
                handle.Set();
            }
        }

        public void Queue(MainLoopTask task)
        {
            Queue(new DelegateTask(delegate { 
                task();
                return null;
            }));
        }

        public void QueueWait(MainLoopTask task)
        {
            QueueWait(delegate { task(); return null; });
        }

        public object QueueWait(MainLoopJob task)
        {
            return QueueWait(new DelegateTask(task));
        }

        private object QueueWait(DelegateTask t)
        {
            if (t.Handle != null)
                t.Handle.Reset();
            else
                t.Handle = new ManualResetEvent(false);
            
            if (Thread.CurrentThread == thread)
                t.Execute();
            else
                Queue(t);

            t.Handle.WaitOne();
            t.Handle.Close();

            return t.Result;
        }

        public uint QueueTimeout(TimeSpan span, TimeoutHandler task)
        {
            return dispatcher.Add(span, delegate {
                return (bool)QueueWait(delegate {
                    TimeSpan s = TimeSpan.Zero;
                    return task(null, ref s); 
                });
            });
        }

        public void CancelQueued(uint handle)
        {
            // FIXME: Get aaron to implement this ;)
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            dispatcher.Dispose();
            handle.Set();
            thread.Join(TimeSpan.FromSeconds(2));
            ((IDisposable)handle).Dispose();
        }
    }
}