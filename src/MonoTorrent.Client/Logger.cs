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

        public static void FlushToDisk()
        {
            Random r = new Random();
            int number = r.Next(0, 10000);
            if (!Directory.Exists(@"C:\logs\" + number))
                Directory.CreateDirectory(@"C:\logs\" + number);

            foreach (KeyValuePair<PeerConnectionID, StringBuilder> keypair in log)
                File.WriteAllText(@"C:\logs\" + number + "\\" + keypair.Key.Peer.Location.GetHashCode(), keypair.Value.ToString());
        }
    }
}
