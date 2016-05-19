#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht
{
    internal class TaskCompleteEventArgs : EventArgs
    {
        private Task task;

        public Task Task
        {
            get { return task; }
            protected internal set { task = value; }
        }

        public TaskCompleteEventArgs(Task task)
        {
            this.task = task;
        }
    }
}

#endif