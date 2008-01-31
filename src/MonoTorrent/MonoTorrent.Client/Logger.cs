using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    public static class Logger
    {
        private static List<TraceListener> listeners;

        static Logger()
        {
            listeners = new List<TraceListener>();
        }

        public static void AddListener(TraceListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            lock (listeners)
                listeners.Add(listener);
        }
        /*
        internal static void Log(PeerIdInternal id, string message)
        {
            Log(id.PublicId, message);
        }

        internal static void Log(PeerId id, string message)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(id.GetHashCode().ToString() + ": " + message);
        }

        internal static void Log(string p)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(p);
        }*/

        internal static void Log(IConnection connection, string message)
        {
            Log(connection, message, null);
        }

        internal static void Log(IConnection connection, string message, params string[] formatting)
        {
        }
    }
}
