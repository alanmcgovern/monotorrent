using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MonoTorrent.Client
{
    public static class Logger
    {
        private static Dictionary<PeerConnectionID, StringBuilder> log;

        static Logger()
        {
            log = new Dictionary<PeerConnectionID, StringBuilder>();
        }

        public static void Log(PeerConnectionID id, string message)
        {
            Trace.WriteLine(id.ToString() + ": " + message);
            return;
            if (!log.ContainsKey(id))
                log.Add(id, new StringBuilder(512));

            log[id].AppendLine(message);
        }

        internal static void Log(string p)
        {
            Trace.WriteLine(p);
        }

        public static void FlushToDisk()
        {

        }
    }
}
