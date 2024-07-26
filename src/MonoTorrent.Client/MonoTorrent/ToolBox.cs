//
// Toolbox.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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

namespace MonoTorrent
{
    static class AsyncInvoker<T>
        where T : EventArgs
    {
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public static void InvokeAsync (EventHandler<T> handler, object sender, T args)
        {
            ThreadPool.UnsafeQueueUserWorkItem (EventInvokerWorkItem.GetOrCreate (handler, sender, args), false);
        }

        internal class EventInvokerWorkItem : IThreadPoolWorkItem
        {
            static readonly Stack<EventInvokerWorkItem> Cache = new Stack<EventInvokerWorkItem> ();

            EventHandler<T>? Handler { get; set; }
            T? EventArgs { get; set; }
            object? Sender { get; set; }

            public static EventInvokerWorkItem GetOrCreate (EventHandler<T> handler, object sender, T args)
            {
                lock (Cache) {
                    if (Cache.Count == 0) {
                        return new EventInvokerWorkItem { Handler = handler, Sender = sender, EventArgs = args };
                    } else {
                        var worker = Cache.Pop ();
                        worker.Handler = handler;
                        worker.Sender = sender;
                        worker.EventArgs = args;
                        return worker;
                    }
                }
            }

            public void Execute ()
            {
                (var handler, var sender, var args) = (Handler, Sender, EventArgs);
                (Handler, Sender, EventArgs) = (null, null, null);
                lock (Cache)
                    if (Cache.Count < 50)
                        Cache.Push (this);
                handler! (sender, args!);
            }
        }
#else
        static readonly ICache<AsyncInvokerState> Cache = new SynchronizedCache<AsyncInvokerState> (() => new AsyncInvokerState ());

        public static void InvokeAsync (EventHandler<T> handler, object sender, T args)
        {
            var state = Cache.Dequeue ().Initialise (handler, sender, args);
            ThreadPool.QueueUserWorkItem (Invoker, state);
        }

        class AsyncInvokerState : ICacheable
        {
            public EventHandler<T>? Handler { get; private set; }
            public T? Args { get; private set; }
            public object? Sender { get; set; }

            public void Initialise ()
                => Initialise (null, null, null);

            public AsyncInvokerState Initialise (EventHandler<T>? handler, object? sender, T? args)
            {
                Handler = handler;
                Sender = sender;
                Args = args;
                return this;
            }
        }

        static readonly WaitCallback Invoker = (object? o) => {
            var state = (AsyncInvokerState) o!;
            state.Handler! (state.Sender, state.Args!);
            Cache.Enqueue (state);
        };
#endif
    }

    static partial class Toolbox
    {
        static readonly Random r = new Random ();

        public static void InvokeAsync<T> (this EventHandler<T> handler, object o, T args)
            where T : EventArgs
        {
            if (handler != null)
                AsyncInvoker<T>.InvokeAsync (handler, o, args);
        }


        /// <summary>
        /// Randomizes the contents of the array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        public static void Randomize<T> (List<T> array)
        {
            var clone = new List<T> (array);
            array.Clear ();

            while (clone.Count > 0) {
                int index = r.Next (0, clone.Count);
                array.Add (clone[index]);
                clone.RemoveAt (index);
            }
        }

    }
}
