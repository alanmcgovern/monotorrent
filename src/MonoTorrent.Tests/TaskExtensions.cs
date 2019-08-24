using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTorrent
{
    public static class TaskExtensions
    {
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
