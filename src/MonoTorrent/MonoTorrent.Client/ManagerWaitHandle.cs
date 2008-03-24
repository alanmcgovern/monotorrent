using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    public class ManagerWaitHandle : WaitHandle
    {
        private List<WaitHandle> handles;
        private string name;

        public string Name
        {
            get { return name; }
        }

        public ManagerWaitHandle(string name)
        {
            this.name = name;
            handles = new List<WaitHandle>();
        }

        public void AddHandle(WaitHandle handle, string name)
        {
            ManagerWaitHandle h = new ManagerWaitHandle(name);
            h.handles.Add(handle);
            handles.Add(h);
        }

        public override bool WaitOne()
        {
            if (handles.Count == 0)
                return true;

            for (int i = 0; i < handles.Count; i++)
                handles[i].WaitOne();

            return true;
        }

        public override bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            return WaitOne(TimeSpan.FromMilliseconds(millisecondsTimeout), exitContext);
        }

        public override bool WaitOne(TimeSpan timeout, bool exitContext)
        {
            if (handles.Count == 0)
                return true;

            for (int i = 0; i < handles.Count; i++)
            {
                int startTime = Environment.TickCount;

                if (!handles[i].WaitOne(timeout, exitContext))
                    return false;
                
                timeout.Subtract(TimeSpan.FromMilliseconds(Environment.TickCount - startTime));
                if (timeout.TotalMilliseconds < 0)
                    return false;
            }

            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (WaitHandle h in handles)
            {
                sb.Append("WaitHandle from: ");
                sb.Append(((ManagerWaitHandle)h).name);
                sb.Append(". State: ");
                sb.Append(h.WaitOne(0, false) ? "Signalled" : "Unsignalled");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
