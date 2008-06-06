using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Tasks
{
    class RegisterTask : Task
    {
        private ClientEngine engine;
        private TorrentManager manager;

        public RegisterTask(ClientEngine engine, TorrentManager manager)
        {
            this.engine = engine;
            this.manager = manager;
        }
        public override void Execute()
        {
            engine.RegisterImpl(manager);
        }
    }
}