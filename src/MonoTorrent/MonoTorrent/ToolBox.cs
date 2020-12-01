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
        static readonly ICache<AsyncInvokerState> Cache = new Cache<AsyncInvokerState> (true).Synchronize ();

        class AsyncInvokerState : ICacheable
        {
            public EventHandler<T> Handler { get; private set; }
            public T Args { get; private set; }
            public object Sender { get; set; }

            public void Initialise ()
                => Initialise (null, null, null);

            public AsyncInvokerState Initialise (EventHandler<T> handler, object sender, T args)
            {
                Handler = handler;
                Sender = sender;
                Args = args;
                return this;
            }
        }

        public static void InvokeAsync (EventHandler<T> handler, object sender, T args)
        {
            var state = Cache.Dequeue ().Initialise (handler, sender, args);
            ThreadPool.QueueUserWorkItem (Invoker, state);
        }

        static readonly WaitCallback Invoker = (object o) => {
            var state = (AsyncInvokerState) o;
            state.Handler (state.Sender, state.Args);
            Cache.Enqueue (state);
        };
    }

    static class Toolbox
    {
        static readonly Random r = new Random ();
        public static int Count<T> (IEnumerable<T> enumerable, Predicate<T> predicate)
        {
            int count = 0;

            foreach (T t in enumerable)
                if (predicate (t))
                    count++;

            return count;
        }

        public static long Accumulate<T> (IEnumerable<T> enumerable, Func<T, long> action)
        {
            long count = 0;

            foreach (T t in enumerable)
                count += action (t);

            return count;
        }

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

        /// <summary>
        /// Checks to see if the contents of two byte arrays are equal
        /// </summary>
        /// <param name="array1">The first array</param>
        /// <param name="array2">The second array</param>
        /// <returns>True if the arrays are equal, false if they aren't</returns>
        public static bool ByteMatch (byte[] array1, byte[] array2)
        {
            if (array1 == null)
                throw new ArgumentNullException (nameof (array1));
            if (array2 == null)
                throw new ArgumentNullException (nameof (array2));

            if (array1.Length != array2.Length)
                return false;

            return ByteMatch (array1, 0, array2, 0, array1.Length);
        }

        /// <summary>
        /// Checks to see if the contents of two byte arrays are equal
        /// </summary>
        /// <param name="array1">The first array</param>
        /// <param name="array2">The second array</param>
        /// <param name="offset1">The starting index for the first array</param>
        /// <param name="offset2">The starting index for the second array</param>
        /// <param name="count">The number of bytes to check</param>
        /// <returns></returns>
        public static bool ByteMatch (byte[] array1, long offset1, byte[] array2, long offset2, long count)
        {
            if (array1 == null)
                throw new ArgumentNullException (nameof (array1));
            if (array2 == null)
                throw new ArgumentNullException (nameof (array2));

            // If either of the arrays is too small, they're not equal
            if ((array1.Length - offset1) < count || (array2.Length - offset2) < count)
                return false;

            // Check if any elements are unequal
            for (int i = 0; i < count; i++)
                if (array1[offset1 + i] != array2[offset2 + i])
                    return false;

            return true;
        }
    }
}
