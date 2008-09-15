//
// TimeoutDispatcher.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using System.Threading;

namespace Mono.Ssdp.Internal
{
    public delegate bool TimeoutHandler ();

    public class TimeoutDispatcher
    {
        private static uint timeout_ids = 1;
        
        private struct TimeoutItem : IComparable<TimeoutItem>
        {
            public uint Id;
            public TimeSpan Timeout;
            public DateTime Trigger;
            public TimeoutHandler Handler;
            
            public int CompareTo (TimeoutItem item)
            {
                return Trigger.CompareTo (item.Trigger);
            }
            
            public override string ToString ()
            {
                return String.Format ("{0} ({1})", Id, Trigger);
            }
        }

        private bool disposed;
        private readonly object wait_mutex = new object ();
        private AutoResetEvent wait;

        private List<TimeoutItem> timeouts = new List<TimeoutItem> ();
        
        public uint Enqueue (TimeSpan timeout, TimeoutHandler handler)
        {
            lock (this) {
                TimeoutItem item = new TimeoutItem ();
                item.Id = timeout_ids++;
                item.Timeout = timeout;
                item.Trigger = DateTime.Now.Add(timeout);
                item.Handler = handler;
                
                Enqueue (ref item);
                
                if (timeouts.Count == 1) {
                    Start ();
                }
                
                return item.Id;
            }
        }
        
        private void Enqueue (ref TimeoutItem item)
        {
            lock (timeouts) {
                int index = timeouts.BinarySearch (item);
                timeouts.Insert (index >= 0 ? index : ~index, item);
                
                if (index == 0 && timeouts.Count > 1) {
                    lock (wait_mutex) {
                        if (wait != null) {
                            wait.Set ();
                        }
                    }
                }
            }
        }
        
        private void Dequeue (uint id)
        {
            lock (timeouts) {
                // FIXME: Comparer for BinarySearch
                for (int i = 0; i < timeouts.Count; i++) {
                    if (timeouts[i].Id == id) {
                        timeouts.RemoveAt (i);
                        return;
                    }
                }
            }
        }
        Thread t;
        private void Start ()
        {
            wait = new AutoResetEvent (false);
            t = new Thread(TimerThread);
            t.Name = "Timer Dispatcher!";
            t.IsBackground = true;
            t.Start();
        }
        
        private void TimerThread (object state)
        {
            while (timeouts.Count > 0) {
                if (disposed)
                    return;

                TimeoutItem item;
                lock (timeouts) {
                    item = timeouts[0];
                }
                
                bool restart = false;
                TimeSpan interval = item.Trigger - DateTime.Now;
                if (interval < TimeSpan.Zero)
                {
                    Dequeue(item.Id);
                    if (item.Handler())
                    {
                        item.Trigger = DateTime.Now.Add(item.Timeout);
                        Enqueue(ref item);
                        restart = true;
                    }
                }
                if (interval >= TimeSpan.Zero) {
                    if (!wait.WaitOne (interval, false)) {
                        Dequeue (item.Id);
                        if(item.Handler ()) {
                            item.Trigger = DateTime.Now.Add(item.Timeout);
                            Enqueue(ref item);
                            restart = true;
                        }
                    } else {
                        restart = true;
                    }
                }
                
                if (restart) {
                    continue;
                }
                
                Dequeue (item.Id);
            }
            
            lock (wait_mutex) {
                if (wait != null) {
                    wait.Set ();
                }
            }
        }

        internal void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Enqueue(TimeSpan.Zero, delegate { return false; });
            t.Join(TimeSpan.FromSeconds(2));
        }
    }
}
