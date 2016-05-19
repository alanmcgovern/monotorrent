#if !DISABLE_DHT
using System;
using MonoTorrent.Dht.Messages;

namespace MonoTorrent.Dht.Tasks
{
    internal class SendQueryTask : Task
    {
        private readonly DhtEngine engine;
        private readonly QueryMessage query;
        private int retries;

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
            Target = node;
            this.retries = retries;
            Retries = retries;
        }

        public int Retries { get; }

        public Node Target { get; }

        public override void Execute()
        {
            if (Active)
                return;
            Hook();
            engine.MessageLoop.EnqueueSend(query, Target);
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
                Target.FailedCount++;
            else
                Target.LastSeen = DateTime.UtcNow;

            if (e.TimedOut && --retries > 0)
            {
                engine.MessageLoop.EnqueueSend(query, Target);
            }
            else
            {
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

#endif