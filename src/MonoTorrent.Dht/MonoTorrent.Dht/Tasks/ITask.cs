using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht.Tasks
{
    interface ITask
    {
        event EventHandler<TaskCompleteEventArgs> Complete;

        bool Active { get; }
        void Cancel ();
        void Execute(DhtEngine engine);
    }
}
