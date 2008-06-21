using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Dht;
using NUnit.Framework;

namespace monotorrent_dht_tests
{
    [TestFixture]
    public class RoutingTableTests
    {
        public static void Main(String[] args)
        {
            RoutingTableTests t = new RoutingTableTests();
            t.Setup();
            t.AddSimilar();
        }
        RoutingTable table;

        [SetUp]
        public void Setup()
        {
            table = new RoutingTable();
        }

        [Test]
        public void AddSimilar()
        {
            for (uint i = 0; i < 35; i++)
            {
                BigInteger b = new BigInteger(i);
                table.Add(new Node(new NodeId(b)));
            }
        }
    }
}
