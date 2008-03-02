using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    internal class ManagerWaitHandle : WaitHandle
    {
        private List<WaitHandle> handles;

        public ManagerWaitHandle()
        {
            handles = new List<WaitHandle>();
        }

        public void AddHandle(WaitHandle handle)
        {
            handles.Add(handle);
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
                if (timeout.TotalMilliseconds <= 0)
                    return false;
            }

            return true;
        }
    }
}
