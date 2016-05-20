using System;

namespace MonoTorrent.Tracker
{
    public class TrackerException : Exception
    {
        public TrackerException()
        {
        }

        public TrackerException(string message)
            : base(message)
        {
        }
    }
}