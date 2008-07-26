using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Dht
{
    class TaskCompleteEventArgs : EventArgs
    {
        private bool successful;

        public bool Successful
        {
            get { return successful; }
        }

        public TaskCompleteEventArgs(bool successful)
        {
            this.successful = successful;
        }
    }
}
