using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    public class PerformanceTests
    {
        static void Time(Action task, string title)
        {
            long start = Environment.TickCount;
            task();
            Console.WriteLine("{0} - {1}ms", title, Environment.TickCount - start);
        }
    }
}
