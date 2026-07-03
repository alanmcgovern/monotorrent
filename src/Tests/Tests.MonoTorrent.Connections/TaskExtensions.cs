using System;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent
{
    static class TaskExtensions
    {
        public static Task<T> WithTimeout<T> (this ReusableTask<T> task, int timeout)
            => task.AsTask ().WithTimeout (timeout);

        public static async Task<T> WithTimeout<T> (this Task<T> task, int timeout)
        {
            var delayTask = Task.Delay (timeout);
            var result = await Task.WhenAny (task, delayTask).ConfigureAwait (false);
            if (result == task)
                return await task.ConfigureAwait (false);

            throw new TimeoutException ($"The task did not complete within {timeout}ms.");
        }
    }
}
