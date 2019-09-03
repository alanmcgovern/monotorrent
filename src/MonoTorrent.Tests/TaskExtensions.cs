using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTorrent
{
    public static class TaskExtensions
    {
        static readonly TimeSpan Timeout = System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (5);

        public static Task WithTimeout (this Task task, string message = null)
            => task.WithTimeout (Timeout, message);

        public static Task WithTimeout (this Task task, int timeout, string message = null)
            => task.WithTimeout (TimeSpan.FromMilliseconds (timeout), message);

        public static async Task WithTimeout (this Task task, TimeSpan timeout, string message = null)
        {
            var result = await Task.WhenAny (task, Task.Delay (timeout));
            if (result == task) {
                await task;
            } else {
                Assert.Fail (message ?? $"The task did not complete within {(int)timeout.TotalMilliseconds}ms.");
                throw new TimeoutException ("This is just to keep the compiler happy :p");
            }
        }

        public static Task<T> WithTimeout<T> (this Task<T> task, string message = null)
            => task.WithTimeout (Timeout, message);

        public static Task<T> WithTimeout<T> (this Task<T> task, int timeout, string message = null)
            => task.WithTimeout (TimeSpan.FromMilliseconds (timeout), message);

        public static async Task<T> WithTimeout<T> (this Task<T> task, TimeSpan timeout, string message = null)
        {
            var result = await Task.WhenAny (task, Task.Delay (timeout));
            if (result == task)
                return await task;

            Assert.Fail (message ?? $"The task did not complete within {(int)timeout.TotalMilliseconds}ms.");
            throw new TimeoutException ("This is just to keep the compiler happy :p");
        }
    }
}
