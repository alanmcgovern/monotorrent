#if !DISABLE_DHT
using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal abstract class Task : ITask
    {
        public event EventHandler<TaskCompleteEventArgs> Completed;

        private bool active;

        public bool Active
        {
            get { return active; }
            protected set { active = value; }
        }

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