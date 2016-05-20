#if !DISABLE_DHT
using System;

namespace MonoTorrent.Dht
{
    internal class TaskCompleteEventArgs : EventArgs
    {
        public TaskCompleteEventArgs(Task task)
        {
            Task = task;
        }

        public Task Task { get; protected internal set; }
    }
}

#endif