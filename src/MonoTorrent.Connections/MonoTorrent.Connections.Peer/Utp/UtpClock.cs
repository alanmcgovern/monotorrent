using System.Diagnostics;

namespace MonoTorrent.Connections.Peer.Utp
{
    static class UtpClock
    {
        public static uint Microseconds {
            get {
                ulong ticks = (ulong) Stopwatch.GetTimestamp ();
                ulong microseconds = ticks * 1_000_000UL / (ulong) Stopwatch.Frequency;
                return unchecked((uint) microseconds);
            }
        }
    }
}
