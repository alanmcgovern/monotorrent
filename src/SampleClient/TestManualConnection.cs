using System;
using System.Threading;

namespace SampleClient
{
    internal class TestManualConnection
    {
        private readonly EngineTestRig rig1;
        private readonly EngineTestRig rig2;

        public TestManualConnection()
        {
            rig1 = new EngineTestRig("Downloads1");
            rig1.Manager.Start();
            rig2 = new EngineTestRig("Downloads2");
            rig2.Manager.Start();

            var p = new ConnectionPair(5151);

            rig1.AddConnection(p.Incoming);
            rig2.AddConnection(p.Outgoing);

            while (true)
            {
                Console.WriteLine("Connection 1A active: {0}", p.Incoming.Connected);
                Console.WriteLine("Connection 2A active: {0}", p.Outgoing.Connected);
                Thread.Sleep(1000);
            }
        }
    }
}