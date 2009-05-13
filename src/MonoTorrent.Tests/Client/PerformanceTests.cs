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
        static void Time(MainLoopTask task, string title)
        {
            long start = Environment.TickCount;
            task();
            Console.WriteLine("{0} - {1}ms", title, Environment.TickCount - start);
        }
		/*
        static void Main(string[] args)
        {
            BitField bf = new BitField(30);
            bf.SetTrue(0, 1, 2, 3);
            bf.SetTrue(25, 27, 28, 29);
            bf = new BitField(bf.ToByteArray(), 30);

            Random r = new Random(19);
            List<ConnectionPair> list = new List<ConnectionPair>();
            TestRig rig = TestRig.CreateSingleFile(700 * 1024 * 1024, 64 * 1024);
            rig.Manager.Settings.MaxConnections = 10000;
            rig.Engine.Settings.GlobalMaxConnections = 10000;
            Time(delegate {
                for (int i = 0; i < 300; i++)
                    list.Add(new ConnectionPair(10000 + i));
            }, "Connections");

            rig.Manager.Start();
            list.ForEach(delegate(ConnectionPair p) { rig.AddConnection(p.Incoming); });
            //Console.ReadLine();
            byte[] bb = new byte[2000];
            Time(delegate {
                for (int i = 0; i < list.Count; i++)
                {
                    IConnection conn = list[i].Outgoing;
                    string id = i.ToString().PadLeft(20, '0');

                    HandshakeMessage handshake = new HandshakeMessage(rig.Manager.InfoHash, id, VersionInfo.ProtocolStringV100, false, false);
                    conn.EndSend(conn.BeginSend(handshake.Encode(), 0, handshake.ByteLength, null, null));

                    r.NextBytes(bb);
                    BitfieldMessage bitfield = new BitfieldMessage(new BitField (bb, rig.Manager.Bitfield.Length));
                    conn.EndSend(conn.BeginSend(bitfield.Encode(), 0, bitfield.ByteLength, null, null));
                }
            }, "Handshaking");

            byte[] buffer = new byte[102400];
            for (int i = 0; i < list.Count; i++)
            {
                IConnection conn = list[i].Outgoing;
                conn.EndReceive(conn.BeginReceive(buffer, 0, buffer.Length, null, null));
            }
            while (true)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    IConnection conn = list[i].Outgoing;
                    conn.EndReceive(conn.BeginReceive(buffer, 0, buffer.Length, null, null));
                }
                break;
            }
            Console.WriteLine("Done");
            Console.ReadLine();
        }*/
    }
}
