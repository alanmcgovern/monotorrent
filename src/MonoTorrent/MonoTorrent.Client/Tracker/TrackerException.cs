using System;
namespace MonoTorrent.Client.Tracker
{
    public class TrackerException : Exception
    {
        public TrackerException ()
        {
        }

        public TrackerException (string message, Exception innerException)
            : base (message, innerException)
        {
        }
    }
}
