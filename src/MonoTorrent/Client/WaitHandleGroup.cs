using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    class WaitHandleGroup : WaitHandle
    {
        private List<WaitHandle> handles;
        private List<string> names;

        public WaitHandleGroup()
        {
            handles = new List<WaitHandle>();
            names = new List<string> ();
        }

        public void AddHandle(WaitHandle handle, string name)
        {
            handles.Add (handle);
            names.Add (name);
        }

        public override void Close ()
        {
            for (int i = 0; i < handles.Count; i++)
                handles [i].Close ();
        }

        public override bool WaitOne()
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll (handles.ToArray ());
        }

        public override bool WaitOne (int millisecondsTimeout)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll (handles.ToArray (), millisecondsTimeout);
        }

        public override bool WaitOne (TimeSpan timeout)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll (handles.ToArray (), timeout);
        }

        public override bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll (handles.ToArray (), millisecondsTimeout, exitContext);
        }

        public override bool WaitOne(TimeSpan timeout, bool exitContext)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll (handles.ToArray (), timeout, exitContext);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < handles.Count; i ++)
            {
                sb.Append("WaitHandle: ");
                sb.Append(names [i]);
                sb.Append(". State: ");
                sb.Append(handles [i].WaitOne(0) ? "Signalled" : "Unsignalled");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}

