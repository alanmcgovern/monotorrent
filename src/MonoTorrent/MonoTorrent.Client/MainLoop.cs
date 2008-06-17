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

namespace MonoTorrent.Client
{
    internal delegate object MainLoopJob();
    internal delegate void MainLoopTask();
    static class MainLoop
    {
        static AutoResetEvent handle = new AutoResetEvent(false);
        static Queue<Task> tasks = new Queue<Task>();
        internal static Thread thread = new Thread(Loop);

        static MainLoop()
        {
            thread.IsBackground = true;
            thread.Start();
        }

        static void Loop()
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

        public static void Queue(Task task)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
                handle.Set();
            }
        }

        internal static void Queue(MainLoopTask task)
        {
            DelegateTask t = new DelegateTask(delegate { 
                task();
                return null;
            });

            Queue(t);
        }

        internal static void QueueWait(MainLoopTask task)
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
    }
}
