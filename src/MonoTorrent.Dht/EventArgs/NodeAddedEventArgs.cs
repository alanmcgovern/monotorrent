#if !DISABLE_DHT
using System;

namespace MonoTorrent.Dht
{
    internal class NodeAddedEventArgs : EventArgs
    {
        public NodeAddedEventArgs(Node node)
        {
            Node = node;
        }

        public Node Node { get; }
    }
}

#endif