//
// TaskExtensions.cs
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
using System.Collections.Concurrent;
using System.Threading.Tasks;

using MonoTorrent.Client;

using NUnit.Framework;

using ReusableTasks;

namespace MonoTorrent
{
    static class TaskExtensions
    {
        internal static readonly TimeSpan Timeout = System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (20);

        public static T TakeWithTimeout<T> (this BlockingCollection<T> collection, string message = null)
        {
            var cts = new System.Threading.CancellationTokenSource (Timeout);
            try {
                return collection.Take (cts.Token);
            } catch {
                Assert.Fail (message);
                throw;
            }
        }

        public static Task WithTimeout (this ReusableTask task, string message = null)
            => task.AsTask ().WithTimeout (Timeout, message);

        public static Task WithTimeout (this ReusableTask task, int timeout, string message = null)
            => task.AsTask ().WithTimeout (timeout, message);

        public static Task WithTimeout (this ReusableTask task, TimeSpan timeout, string message = null)
            => task.AsTask ().WithTimeout (timeout, message);

        public static Task WithTimeout (this Task task, string message = null)
            => task.WithTimeout (Timeout, message);

        public static Task WithTimeout (this Task task, int timeout, string message = null)
            => task.WithTimeout (TimeSpan.FromMilliseconds (timeout), message);

        public static async Task WithTimeout (this Task task, TimeSpan timeout, string message = null)
        {
            var cancellation = new System.Threading.CancellationTokenSource ();
            var delayTask = Task.Delay (timeout, cancellation.Token);
            var result = await Task.WhenAny (task, delayTask).ConfigureAwait (false);

            cancellation.Cancel ();
            try { await delayTask.ConfigureAwait (false); } catch (OperationCanceledException) { }

            if (result == task) {
                await task;
            } else {
                throw new TimeoutException (message ?? $"The task did not complete within {(int) timeout.TotalMilliseconds}ms.");
            }
        }

        public static Task<T> WithTimeout<T> (this ReusableTask<T> task, string message = null)
            => task.AsTask ().WithTimeout (message);

        public static Task<T> WithTimeout<T> (this ReusableTask<T> task, int timeout, string message = null)
            => task.AsTask ().WithTimeout (TimeSpan.FromMilliseconds (timeout), message);

        public static Task<T> WithTimeout<T> (this Task<T> task, string message = null)
            => task.WithTimeout (Timeout, message);

        public static Task<T> WithTimeout<T> (this Task<T> task, int timeout, string message = null)
            => task.WithTimeout (TimeSpan.FromMilliseconds (timeout), message);

        public static async Task<T> WithTimeout<T> (this Task<T> task, TimeSpan timeout, string message = null)
        {
            var cancellation = new System.Threading.CancellationTokenSource ();
            var delayTask = Task.Delay (timeout, cancellation.Token);
            var result = await Task.WhenAny (task, delayTask).ConfigureAwait (false);

            cancellation.Cancel ();
            try { await delayTask.ConfigureAwait (false); } catch (OperationCanceledException) { }

            if (result == task) {
                return await task.ConfigureAwait (false);
            }
            throw new TimeoutException (message ?? $"The task did not complete within {(int) timeout.TotalMilliseconds}ms.");
        }
    }
}
