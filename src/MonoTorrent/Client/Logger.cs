using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    public static class Logger
    {
        private static readonly List<TraceListener> listeners;

        private static readonly StringBuilder sb = new StringBuilder();

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

        public static void Flush()
        {
            lock (listeners)
                listeners.ForEach(delegate(TraceListener l) { l.Flush(); });
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

        [Conditional("DO_NOT_ENABLE")]
        internal static void Log(IConnection connection, string message)
        {
            Log(connection, message, null);
        }

        [Conditional("DO_NOT_ENABLE")]
        internal static void Log(IConnection connection, string message, params object[] formatting)
        {
            lock (listeners)
            {
                sb.Remove(0, sb.Length);
                sb.Append(Environment.TickCount);
                sb.Append(": ");

                if (connection != null)
                    sb.Append(connection.EndPoint);

                if (formatting != null)
                    sb.Append(string.Format(message, formatting));
                else
                    sb.Append(message);
                var s = sb.ToString();
                listeners.ForEach(delegate(TraceListener l) { l.WriteLine(s); });
            }
        }
    }
}