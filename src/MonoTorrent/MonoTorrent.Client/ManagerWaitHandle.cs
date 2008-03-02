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
            return WaitHandle.WaitAll(handles.ToArray());
        }

        public override bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll(handles.ToArray(), millisecondsTimeout, exitContext);
        }

        public override bool WaitOne(TimeSpan timeout, bool exitContext)
        {
            if (handles.Count == 0)
                return true;
            return WaitHandle.WaitAll(handles.ToArray(), timeout, exitContext);
        }
    }
}
