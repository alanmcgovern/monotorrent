using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht.Tasks
{
    abstract class Task : ITask
    {
        public event EventHandler<TaskCompleteEventArgs> Complete;

        private bool active;

        public bool Active
        {
            get { return active; }
            protected set { active = value; }
        }

        public virtual void Cancel()
        {
            active = false;
            RaiseComplete(new TaskCompleteEventArgs(false));
        }

        public abstract void Execute(DhtEngine engine);

        protected virtual void RaiseComplete(TaskCompleteEventArgs e)
        {
            EventHandler<TaskCompleteEventArgs> h = Complete;
            if(h != null)
                h(this, e);
        }
    }
}
