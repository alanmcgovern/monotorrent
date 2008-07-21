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
using MonoTorrent.Client.Tasks;
using Mono.Ssdp.Internal;

namespace MonoTorrent.Client
{
    public delegate object MainLoopJob();
    public delegate void MainLoopTask();
    public class MainLoop
    {
        TimeoutDispatcher dispatcher = new TimeoutDispatcher();
        AutoResetEvent handle = new AutoResetEvent(false);
        Queue<Task> tasks = new Queue<Task>();
        internal Thread thread;

        public MainLoop()
        {
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "MainLoop";
            thread.Start();
        }

        void Loop()
        {
            while (true)
            {
                Task task = null;

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
                    task.Execute();
                }
            }
        }

        private void Queue(Task task)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
                handle.Set();
            }
        }

        public void Queue(MainLoopTask task)
        {
            DelegateTask t = new DelegateTask(delegate { 
                task();
                return null;
            });

            Queue(t);
        }

        public void QueueWait(MainLoopTask task)
        {
            DelegateTask t = new DelegateTask(delegate { task(); return null; });
            t.Handle = new ManualResetEvent(false);
            
            if (Thread.CurrentThread == thread)
                t.Execute();
            else
                Queue(t);

            t.Handle.WaitOne();
            t.Handle.Close();
        }

        public uint QueueTimeout(TimeSpan span, MainLoopTask task)
        {
            return QueueTimeout(span, task, false);
        }

        public uint QueueTimeout(TimeSpan span, MainLoopTask task, bool autoRepeat)
        {
            return dispatcher.Enqueue(span, delegate {
                Queue(task);
                return autoRepeat;
            });
        }

        public void CancelQueued(uint handle)
        {
            // FIXME: Get aaron to implement this ;)
        }
    }
}