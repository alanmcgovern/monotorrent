using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Connections.Peer.Utp
{
    sealed class UtpConnectionScheduler : IDisposable
    {
        sealed class ScheduledConnection
        {
            public uint? Deadline { get; set; }
            public long Version { get; set; }
        }

        readonly struct HeapEntry
        {
            public HeapEntry (UtpPeerConnection connection, uint deadline, long version)
            {
                Connection = connection;
                Deadline = deadline;
                Version = version;
            }

            public UtpPeerConnection Connection { get; }
            public uint Deadline { get; }
            public long Version { get; }
        }

        readonly Dictionary<UtpPeerConnection, ScheduledConnection> connections = new ();
        readonly List<HeapEntry> deadlines = new ();
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
            lock (locker) {
                if (disposed)
                    return;

                var scheduled = new ScheduledConnection ();
                connections[connection] = scheduled;
                UpdateDeadlineLocked (connection, scheduled);
                UpdateTimerLocked ();
            }
        }

        public void Unregister (UtpPeerConnection connection)
        {
            lock (locker) {
                if (connections.Remove (connection, out var scheduled))
                    scheduled.Version++;

                UpdateTimerLocked ();
            }
        }

        public void Reschedule (UtpPeerConnection connection)
        {
            lock (locker) {
                if (disposed || !connections.TryGetValue (connection, out var scheduled))
                    return;

                if (UpdateDeadlineLocked (connection, scheduled))
                    UpdateTimerLocked ();
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
            List<UtpPeerConnection> dueConnections = new ();
            lock (locker) {
                if (disposed || running)
                    return;

                var now = clock.Microseconds;
                while (TryPeekValidDeadlineLocked (out var entry) && (IsDue (entry.Deadline, now) || (forcedDeadline.HasValue && !IsBefore (forcedDeadline.Value, entry.Deadline)))) {
                    PopHeapLocked ();
                    if (!connections.TryGetValue (entry.Connection, out var scheduled) || scheduled.Version != entry.Version || scheduled.Deadline != entry.Deadline)
                        continue;

                    scheduled.Deadline = null;
                    dueConnections.Add (entry.Connection);
                }

                if (dueConnections.Count == 0) {
                    UpdateTimerLocked ();
                    return;
                }

                running = true;
            }

            try {
                foreach (var connection in dueConnections)
                    await connection.ProcessScheduledEventsAsync (forcedDeadline);
            } finally {
                lock (locker) {
                    running = false;
                    UpdateTimerLocked ();
                }
            }
        }

        bool UpdateDeadlineLocked (UtpPeerConnection connection, ScheduledConnection scheduled)
        {
            var deadline = connection.NextScheduledEventMicroseconds;
            if (scheduled.Deadline == deadline)
                return false;

            scheduled.Version++;
            scheduled.Deadline = deadline;
            if (scheduled.Deadline.HasValue)
                PushHeapLocked (new HeapEntry (connection, scheduled.Deadline.Value, scheduled.Version));
            return true;
        }

        internal int HeapEntryCountForTests {
            get {
                lock (locker)
                    return deadlines.Count;
            }
        }

        void UpdateTimerLocked ()
        {
            if (disposed)
                return;

            if (TryPeekValidDeadlineLocked (out var entry)) {
                scheduledDeadline = entry.Deadline;
                timer.Change (DueTime (clock.Microseconds, entry.Deadline), Timeout.InfiniteTimeSpan);
            } else {
                scheduledDeadline = null;
                timer.Change (Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        bool TryPeekValidDeadlineLocked (out HeapEntry entry)
        {
            while (deadlines.Count > 0) {
                entry = deadlines[0];
                if (connections.TryGetValue (entry.Connection, out var scheduled) && scheduled.Version == entry.Version && scheduled.Deadline == entry.Deadline)
                    return true;

                PopHeapLocked ();
            }

            entry = default;
            return false;
        }

        void PushHeapLocked (HeapEntry entry)
        {
            deadlines.Add (entry);
            int index = deadlines.Count - 1;
            while (index > 0) {
                int parent = (index - 1) / 2;
                if (!IsBefore (deadlines[index].Deadline, deadlines[parent].Deadline))
                    break;

                (deadlines[parent], deadlines[index]) = (deadlines[index], deadlines[parent]);
                index = parent;
            }
        }

        HeapEntry PopHeapLocked ()
        {
            var result = deadlines[0];
            var last = deadlines[^1];
            deadlines.RemoveAt (deadlines.Count - 1);
            if (deadlines.Count == 0)
                return result;

            deadlines[0] = last;
            int index = 0;
            while (true) {
                int left = index * 2 + 1;
                if (left >= deadlines.Count)
                    break;

                int right = left + 1;
                int smallest = right < deadlines.Count && IsBefore (deadlines[right].Deadline, deadlines[left].Deadline) ? right : left;
                if (!IsBefore (deadlines[smallest].Deadline, deadlines[index].Deadline))
                    break;

                (deadlines[index], deadlines[smallest]) = (deadlines[smallest], deadlines[index]);
                index = smallest;
            }

            return result;
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
            deadlines.Clear ();
        }
    }
}
