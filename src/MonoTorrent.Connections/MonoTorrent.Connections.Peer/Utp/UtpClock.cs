using System.Diagnostics;

namespace MonoTorrent.Connections.Peer.Utp
{
    interface IUtpClock
    {
        uint Microseconds { get; }
    }

    sealed class StopwatchUtpClock : IUtpClock
    {
        public static StopwatchUtpClock Instance { get; } = new ();

        StopwatchUtpClock ()
        {
        }

        public uint Microseconds {
            get {
                ulong ticks = (ulong) Stopwatch.GetTimestamp ();
                ulong microseconds = ticks * 1_000_000UL / (ulong) Stopwatch.Frequency;
                return unchecked((uint) microseconds);
            }
        }
    }
}
