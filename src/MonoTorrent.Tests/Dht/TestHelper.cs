using System.Collections.Generic;
using System.Net;

namespace MonoTorrent.Dht
{
    static class TestHelper
    {
        internal static void ManyNodes (out RoutingTable routingTable, out List<NodeId> nodes)
        {
            // Generate our local id
            var id = new byte[20];
            id[19] = 7;

            nodes = new List<NodeId> ();
            var table = new RoutingTable (new Node (new NodeId (id), new IPEndPoint (IPAddress.Any, 0)));

            for (var i = 0; i <= 30; i++) {
                if (i == 7)
                    continue;

                id = new byte[20];
                id[19] = (byte) i;
                nodes.Add (new NodeId (id));
                table.Add (new Node (new NodeId (id), new IPEndPoint (IPAddress.Any, 0)));
            }

            nodes.Sort (delegate (NodeId left, NodeId right) {
                var dLeft = left ^ table.LocalNode.Id;
                var dRight = right ^ table.LocalNode.Id;
                return dLeft.CompareTo (dRight);
            });

            nodes.RemoveAll (n => table.FindNode (n) == null);
            routingTable = table;
        }
    }
}
