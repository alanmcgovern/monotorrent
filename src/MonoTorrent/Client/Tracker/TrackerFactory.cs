using System;
using System.Collections.Generic;

namespace MonoTorrent.Client.Tracker
{
    public static class TrackerFactory
    {
        private static readonly Dictionary<string, Type> trackerTypes = new Dictionary<string, Type>();

        static TrackerFactory()
        {
            // Register builtin tracker clients
            Register("udp", typeof(UdpTracker));
            Register("http", typeof(HTTPTracker));
            Register("https", typeof(HTTPTracker));
        }

        public static void Register(string protocol, Type trackerType)
        {
            if (string.IsNullOrEmpty(protocol))
                throw new ArgumentException("cannot be null or empty", protocol);

            if (trackerType == null)
                throw new ArgumentNullException("trackerType");

            lock (trackerTypes)
                trackerTypes.Add(protocol, trackerType);
        }

        public static Tracker Create(Uri uri)
        {
            Check.Uri(uri);

            if (!trackerTypes.ContainsKey(uri.Scheme))
                return null;

            try
            {
                return (Tracker) Activator.CreateInstance(trackerTypes[uri.Scheme], uri);
            }
            catch
            {
                return null; // Invalid tracker
            }
        }
    }
}