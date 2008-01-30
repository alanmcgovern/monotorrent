using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MonoTorrent.Client
{
    public static class Logger
    {
        private static Dictionary<PeerIdInternal, LinkedList<string>> log;
        private static List<TraceListener> listeners;

        static Logger()
        {
            listeners = new List<TraceListener>();
            log = new Dictionary<PeerIdInternal, LinkedList<string>>();
        }

        public static void AddListener(TraceListener listener)
        {
            lock (listeners)
                listeners.Add(listener);
        }

        [Conditional("EnableLogging")]
        internal static void Log(PeerIdInternal id, string message)
        {
            Log(id.PublicId, message);
        }

        [Conditional("EnableLogging")]
        public static void Log(PeerId id, string message)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(id.GetHashCode().ToString() + ": " + message);
            /*
            if (!log.ContainsKey(id))
                log.Add(id, new LinkedList<string>());

            if (log[id].Count >= 50)
                log[id].RemoveFirst();

            log[id].AddLast(DateTime.Now.ToLongTimeString() + ": " + message);
             */
        }

        [Conditional("EnableLogging")]
        internal static void Log(string p)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(p);
        }

        [Conditional("EnableLogging")]
        public static void FlushToDisk()
        {/*
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
            }*/
        }

        internal static void FlushToDisk(PeerIdInternal id)
        {/*
            if (!Directory.Exists(@"C:\Logs\"))
                Directory.CreateDirectory(@"C:\Logs\");

            LinkedList<string> data = log[id];

            using (FileStream s = new FileStream(@"C:\Logs\" + id.GetHashCode() + ".txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            using (StreamWriter output = new StreamWriter(s))
            {
                foreach (string str in data)
                    output.WriteLine(str);
            }
          */
        }
    }
}
