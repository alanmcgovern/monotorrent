using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht
{
    interface ITask
    {
        event EventHandler<TaskCompleteEventArgs> Completed;

        bool Active { get; }
        void Execute();
    }
}
