using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace MonoTorrent.Dht.Tests
{
	static class TestHelper
	{
        internal static void ManyNodes(out RoutingTable routingTable, out List<NodeId> nodes)
        {
            // Generate our local id
            byte[] id = new byte[20];
            id[19] = 7;

            nodes = new List<NodeId>();
            RoutingTable table = new RoutingTable(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));

            for (int i = 0; i <= 30; i++)
            {
                if (i == 7)
                    continue;

                id = new byte[20];
                id[19] = (byte)i;
                nodes.Add(new NodeId(id));
                table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            nodes.Sort(delegate(NodeId left, NodeId right)
            {
                NodeId dLeft = left.Xor(table.LocalNode.Id);
                NodeId dRight = right.Xor(table.LocalNode.Id);
                return dLeft.CompareTo(dRight);
            });
            
            nodes.RemoveAll(delegate(NodeId n) { return table.FindNode(n) == null; });
            routingTable = table;
        }
	}
}
