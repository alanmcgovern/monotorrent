#if !DISABLE_DHT
using System;

namespace MonoTorrent.Dht
{
    internal interface ITask
    {
        bool Active { get; }
        event EventHandler<TaskCompleteEventArgs> Completed;
        void Execute();
    }
}

#endif