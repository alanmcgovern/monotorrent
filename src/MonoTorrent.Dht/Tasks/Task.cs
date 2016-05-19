#if !DISABLE_DHT
using System;

namespace MonoTorrent.Dht
{
    internal abstract class Task : ITask
    {
        public event EventHandler<TaskCompleteEventArgs> Completed;

        public bool Active { get; protected set; }

        public abstract void Execute();

        protected virtual void RaiseComplete(TaskCompleteEventArgs e)
        {
            var h = Completed;
            if (h != null)
                h(this, e);
        }
    }
}

#endif