using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Connections.Peer.Utp
{
    sealed class UtpConnectionScheduler : IDisposable
    {
        readonly ConcurrentDictionary<UtpPeerConnection, byte> connections = new ();
        readonly IUtpClock clock;
        readonly bool forceTimerDeadlines;
        readonly Timer timer;
        readonly object locker = new ();
        uint? scheduledDeadline;
        bool disposed;
        bool running;

        public UtpConnectionScheduler (IUtpClock clock)
        {
            this.clock = clock;
            forceTimerDeadlines = clock is not StopwatchUtpClock;
            timer = new Timer (Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Register (UtpPeerConnection connection)
        {
            if (disposed)
                return;

            connections[connection] = 0;
            Reschedule ();
        }

        public void Unregister (UtpPeerConnection connection)
        {
            connections.TryRemove (connection, out _);
            Reschedule ();
        }

        public void Reschedule ()
        {
            if (disposed)
                return;

            uint now = clock.Microseconds;
            uint? next = null;
            foreach (var connection in connections.Keys) {
                var deadline = connection.NextScheduledEventMicroseconds;
                if (!deadline.HasValue)
                    continue;

                if (!next.HasValue || IsBefore (deadline.Value, next.Value))
                    next = deadline.Value;
            }

            var due = next.HasValue ? DueTime (now, next.Value) : Timeout.InfiniteTimeSpan;
            lock (locker) {
                if (!disposed) {
                    scheduledDeadline = next;
                    timer.Change (due, Timeout.InfiniteTimeSpan);
                }
            }
        }

        internal Task ProcessDueEventsForTests ()
            => ProcessDueEventsAsync (null);

        async void Tick (object? state)
        {
            uint? forcedDeadline;
            lock (locker)
                forcedDeadline = forceTimerDeadlines ? scheduledDeadline : null;
            await ProcessDueEventsAsync (forcedDeadline);
        }

        async Task ProcessDueEventsAsync (uint? forcedDeadline)
        {
            lock (locker) {
                if (disposed || running)
                    return;
                running = true;
            }

            try {
                var now = clock.Microseconds;
                foreach (var connection in connections.Keys) {
                    var deadline = connection.NextScheduledEventMicroseconds;
                    if (deadline.HasValue && (IsDue (deadline.Value, now) || (forcedDeadline.HasValue && !IsBefore (forcedDeadline.Value, deadline.Value))))
                        await connection.ProcessScheduledEventsAsync (forcedDeadline);
                }
            } finally {
                lock (locker)
                    running = false;
                Reschedule ();
            }
        }

        static TimeSpan DueTime (uint now, uint deadline)
        {
            if (IsDue (deadline, now))
                return TimeSpan.Zero;

            var microseconds = unchecked(deadline - now);
            var milliseconds = Math.Max (1, (int) Math.Min (int.MaxValue, (microseconds + 999UL) / 1000UL));
            return TimeSpan.FromMilliseconds (milliseconds);
        }

        static bool IsDue (uint deadline, uint now)
            => unchecked(now - deadline) < 0x8000_0000u;

        static bool IsBefore (uint left, uint right)
            => unchecked(left - right) >= 0x8000_0000u;

        public void Dispose ()
        {
            lock (locker) {
                if (disposed)
                    return;
                disposed = true;
            }
            timer.Dispose ();
            connections.Clear ();
        }
    }
}
