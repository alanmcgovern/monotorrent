using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tasks
{
    class DelegateTask : Task
    {
        private MainLoopJob task;
        private object result;

        public object Result
        {
            get { return result; }
        }
        public DelegateTask(MainLoopJob task)
        {
            this.task = task;
        }

        public override void Execute()
        {
            result = task();
        }
    }
}
