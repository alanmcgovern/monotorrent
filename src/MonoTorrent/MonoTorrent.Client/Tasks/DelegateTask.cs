using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client.Tasks
{
    class DelegateTask : Task
    {
        public ManualResetEvent Handle;
        private object result;
        private MainLoopJob task;

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
            if (Handle != null)
                Handle.Set();
        }
    }
}
