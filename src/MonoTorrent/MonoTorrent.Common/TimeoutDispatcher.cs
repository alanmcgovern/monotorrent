using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Mono.Ssdp.Internal
{
    public delegate bool TimeoutHandler(object state, ref TimeSpan interval);

    public class TimeoutDispatcher : IDisposable
    {
        private static long _timeoutIds;

        private readonly ConcurrentDictionary<long, TimeoutItem> _timeouts =
            new ConcurrentDictionary<long, TimeoutItem>();

        private bool _disposed;

        #region IDisposable Members

        public void Dispose()
        {
            if (_disposed)
                return;
            Clear();
            _disposed = true;
        }

        #endregion

        private void TimerCallback(object state)
        {
            lock (state)
            {
                var item = (TimeoutItem) state;
                if (item.Handler == null)
                {
                    _timeouts.TryRemove(item.Id, out item);
                    return;
                }
                var oldTimeout = item.Timeout;
                var requeue = item.Handler(item.State, ref item.Timeout);
                if (!requeue)
                {
                    item.Handler = null;
                    item.Timer.Dispose();
                    _timeouts.TryRemove(item.Id, out item);
                }
                else if (oldTimeout != item.Timeout)
                    item.Timer.Change(item.Timeout, item.Timeout);
            }
        }

        public long Add(uint timeoutMs, TimeoutHandler handler)
        {
            return Add(timeoutMs, handler, null);
        }

        public long Add(TimeSpan timeout, TimeoutHandler handler)
        {
            return Add(timeout, handler, null);
        }

        public long Add(uint timeoutMs, TimeoutHandler handler, object state)
        {
            return Add(TimeSpan.FromMilliseconds(timeoutMs), handler, state);
        }

        public long Add(TimeSpan timeout, TimeoutHandler handler, object state)
        {
            CheckDisposed();
            if (timeout == TimeSpan.Zero)
                timeout = TimeSpan.FromMilliseconds(1);
            var item = new TimeoutItem
                {Id = Interlocked.Increment(ref _timeoutIds), Timeout = timeout, Handler = handler, State = state};
            item.Timer = new Timer(TimerCallback, item, timeout, timeout);

            Add(ref item);

            return item.Id;
        }

        private void Add(ref TimeoutItem item)
        {
            _timeouts.TryAdd(item.Id, item);
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(ToString());
        }

        private void Clear()
        {
            foreach (TimeoutItem item in _timeouts.Values)
                item.Timer.Dispose();
            _timeouts.Clear();
        }

        #region Nested type: TimeoutItem

        private class TimeoutItem
        {
            public TimeoutHandler Handler;
            public long Id;
            public object State;
            public TimeSpan Timeout;
            public Timer Timer;

            public override string ToString()
            {
                return String.Format("{0} ({1})", Id, Timeout);
            }
        }

        #endregion
    }
}