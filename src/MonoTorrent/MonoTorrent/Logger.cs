//
// Logger.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using MonoTorrent.Client.Connections;

namespace MonoTorrent
{
    static class Logger
    {
        static readonly List<TraceListener> listeners;

        static Logger ()
        {
            listeners = new List<TraceListener> ();
        }

        public static void AddListener (TraceListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException (nameof (listener));

            lock (listeners)
                listeners.Add (listener);
        }

        public static void Flush ()
        {
            lock (listeners)
                listeners.ForEach (delegate (TraceListener l) { l.Flush (); });
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

        [Conditional ("DO_NOT_ENABLE")]
        internal static void Log (IConnection connection, string message)
        {
            Log (connection, message, null);
        }

        static readonly StringBuilder sb = new StringBuilder ();
        [Conditional ("DO_NOT_ENABLE")]
        internal static void Log (IConnection connection, string message, params object[] formatting)
        {
            lock (listeners) {
                sb.Remove (0, sb.Length);
                sb.Append (Environment.TickCount);
                sb.Append (": ");

                if (connection != null)
                    sb.Append (connection.EndPoint);

                sb.Append (formatting != null ? string.Format (message, formatting) : message);
                string s = sb.ToString ();
                listeners.ForEach (delegate (TraceListener l) { l.WriteLine (s); });
            }
        }
    }
}
