using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client
{
    static class MessageQueueExtensions
    {
        public static PeerMessage TryDequeue (this MessageQueue queue)
            => queue.TryDequeue (out PeerMessage message, out PeerMessage.Releaser releaser) ? message : null;
    }
}
