using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;

namespace SampleClient
{
    public class CustomConnection : IConnection
    {
        private string name;
        private Socket s;
        private bool incoming;
        public CustomConnection(Socket s, bool incoming, string name)
        {
            this.name = name;
            this.s = s;
            this.incoming = incoming;
        }
        public override string ToString()
        {
            return name;
        }
        public byte[] AddressBytes
        {
            get { return ((IPEndPoint)s.RemoteEndPoint).Address.GetAddressBytes(); }
        }

        public bool Connected
        {
            get { return s.Connected; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool IsIncoming
        {
            get { return incoming; }
        }

        public System.Net.EndPoint EndPoint
        {
            get { return s.RemoteEndPoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            throw new InvalidOperationException();
        }

        public void EndConnect(IAsyncResult result)
        {
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", name, "received");
            return s.EndReceive(result);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", name, "sent");
            return s.EndSend(result);
        }

        public void Dispose()
        {
            s.Close();
        }
    }

    public class CustomListener : ConnectionListenerBase
    {
        public override void Dispose()
        {

        }

        public override void Start()
        {

        }

        public override void Stop()
        {

        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("tcp://12.123.123.1:2342"), new NoEncryption());
            base.RaiseConnectionReceived(p, connection, manager);
        }
    }


    class TestManualConnection
    {
        ClientEngine engine1;
        ClientEngine engine2;
        TorrentManager manager1;
        TorrentManager manager2;
        IConnection connection1a;
        IConnection connection1b;
        //IConnection connection2a;
        //IConnection connection2b;
        CustomListener listener1;
        CustomListener listener2;
        Torrent torrent;

        Socket s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket s1b = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //Socket s2a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //Socket s2b = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public TestManualConnection()
        {
            listener1 = new CustomListener();
            listener2 = new CustomListener();

            engine1 = new ClientEngine(EngineSettings.DefaultSettings(), listener1);
            engine2 = new ClientEngine(EngineSettings.DefaultSettings(), listener2);

            torrent = Torrent.Load("Torrents/untitled.bmp.torrent");
            manager1 = new TorrentManager(torrent, "Downloads", TorrentSettings.DefaultSettings());
            manager2 = new TorrentManager(torrent, "Downloads2", TorrentSettings.DefaultSettings());

            engine1.Register(manager1);
            engine2.Register(manager2);

            //engine1.ConnectionManager.PeerMessageTransferred += delegate(object sender, PeerMessageEventArgs e) { Console.WriteLine(e.Message.ToString()); };
            //engine2.ConnectionManager.PeerMessageTransferred += delegate(object sender, PeerMessageEventArgs e) { Console.WriteLine(e.Message.ToString()); };

            manager1.Start();
            manager2.Start();

            TcpListener socketListener = new TcpListener(1220);
            socketListener.Start();
            s1a.Connect(IPAddress.Loopback, 1220);
            s1b = socketListener.AcceptSocket();

            //s2a.Connect(IPAddress.Loopback, 1220);
            //s2b = socketListener.AcceptSocket();

            connection1a = new CustomConnection(s1a, true, "1A");
            connection1b = new CustomConnection(s1b, false, "1B");

            //connection2a = new CustomConnection(s2a, true, "2A");
            //connection2b = new CustomConnection(s2b, false, "2B");

            listener1.Add(manager1, connection1a);
            //listener1.Add(manager1, connection2a);
            listener2.Add(manager2, connection1b);
            //listener2.Add(manager2, connection2b);

            while (true)
            {
                Console.WriteLine("Connection 1A active: {0}", connection1a.Connected);
                //Console.WriteLine("Connection 1B active: {0}", connection2a.Connected);
                Console.WriteLine("Connection 2A active: {0}", connection1b.Connected);
                //Console.WriteLine("Connection 2B active: {0}", connection2b.Connected);
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static BEncodedDictionary CreateTorrent()
        {
            BEncodedDictionary infoDict = new BEncodedDictionary();
            infoDict[new BEncodedString("piece length")] = new BEncodedNumber(256 * 1024);
            infoDict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * 15]);
            infoDict[new BEncodedString("length")] = new BEncodedNumber(15 * 256 * 1024 - 1);
            infoDict[new BEncodedString("name")] = new BEncodedString("test.files");

            BEncodedDictionary dict = new BEncodedDictionary();
            dict[new BEncodedString("info")] = infoDict;

            BEncodedList announceTier = new BEncodedList();
            announceTier.Add(new BEncodedString(String.Format("http://transfers/{0}", new byte[20])));
            BEncodedList announceList = new BEncodedList();
            announceList.Add(announceTier);
            dict[new BEncodedString("announce-list")] = announceList;

            return dict;
        }


    }

}
