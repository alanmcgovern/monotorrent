using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    class SendQueryTask : Task
    {
        private DhtEngine engine;
        private Node node;
        private QueryMessage query;
        private int retries;
        private int origRetries;

        public int Retries
        {
            get { return origRetries; }
        }

        public Node Target
        {
            get { return node; } 
        }

        public SendQueryTask(DhtEngine engine, QueryMessage query, Node node)
            : this(engine, query, node, 3)
        {

        }

        public SendQueryTask(DhtEngine engine, QueryMessage query, Node node, int retries)
        {
            if (engine == null)
                throw new ArgumentNullException("engine");
            if (query == null)
                throw new ArgumentNullException("message");
            if (node == null)
                throw new ArgumentNullException("message");

            this.engine = engine;
            this.query = query;
            this.node = node;
            this.retries = retries;
            this.origRetries = retries;
        }

        public override void Execute()
        {
            if (Active)
                return;
            Hook();
            engine.MessageLoop.EnqueueSend(query, node);
        }

        private void Hook()
        {
            engine.MessageLoop.QuerySent += MessageSent;
        }

        private void MessageSent(object sender, SendQueryEventArgs e)
        {
            if (e.Query != query)
                return;

            // If the message timed out and we we haven't already hit the maximum retries
            // send again. Otherwise we propagate the eventargs through the Complete event.
            if (e.TimedOut)
                node.FailedCount++;

            if (e.TimedOut && --retries > 0)
            {
                engine.MessageLoop.EnqueueSend(query, node);
            }
            else
            {
                Target.LastSeen = DateTime.UtcNow;
                RaiseComplete(e);
            }
        }

        protected override void RaiseComplete(TaskCompleteEventArgs e)
        {
            Unhook();
            e.Task = this;
            base.RaiseComplete(e);
        }

        private void Unhook()
        {
            engine.MessageLoop.QuerySent -= MessageSent;
        }
    }
}
