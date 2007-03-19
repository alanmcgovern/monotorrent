using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MonoTorrent.Client
{
    public static class Logger
    {
        private static Dictionary<PeerConnectionID, LinkedList<string>> log;

        static Logger()
        {
            log = new Dictionary<PeerConnectionID, LinkedList<string>>();
        }

        [Conditional("EnableLogging")]
        public static void Log(PeerConnectionID id, string message)
        {
            if (!log.ContainsKey(id))
                log.Add(id, new LinkedList<string>());

            if (log[id].Count >= 50)
                log[id].RemoveFirst();

            log[id].AddLast(DateTime.Now.ToLongTimeString() + ": " + message);
        }

        [Conditional("EnableLogging")]
        internal static void Log(string p)
        {
            Trace.WriteLine(p);
        }

        [Conditional("EnableLogging")]
        public static void FlushToDisk()
        {
            if (!Directory.Exists(@"C:\Logs\"))
                Directory.CreateDirectory(@"C:\Logs\");

            foreach (KeyValuePair<PeerConnectionID, LinkedList<string>> keypair in log)
            {
                using (FileStream s = new FileStream(@"C:\Logs\" + keypair.Key.GetHashCode() + ".txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                using(StreamWriter output = new StreamWriter(s))
                {
                    foreach (string str in keypair.Value)
                        output.WriteLine(str);
                }
            }
        }

        internal static void FlushToDisk(PeerConnectionID id)
        {
            if (!Directory.Exists(@"C:\Logs\"))
                Directory.CreateDirectory(@"C:\Logs\");

            LinkedList<string> data = log[id];

            using (FileStream s = new FileStream(@"C:\Logs\" + id.GetHashCode() + ".txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            using (StreamWriter output = new StreamWriter(s))
            {
                foreach (string str in data)
                    output.WriteLine(str);
            }
        }
    }
}
