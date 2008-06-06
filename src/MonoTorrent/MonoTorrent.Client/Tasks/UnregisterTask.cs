using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tasks
{
    class UnregisterTask : Task
    {
        private ClientEngine engine;
        private TorrentManager manager;

        public UnregisterTask(ClientEngine engine, TorrentManager manager)
        {
            this.engine = engine;
            this.manager = manager;
        }
        public override void Execute()
        {
            engine.UnregisterImpl(manager);
        }
    }
}
