using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Common;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Connections;

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

        public EndPoint EndPoint
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

    public class EngineTestRig
    {
        private ClientEngine engine;
        private CustomListener listener;
        private TorrentManager manager;
        private Torrent torrent;

        public ClientEngine Engine
        {
            get { return engine; }
        }

        public CustomListener Listener
        {
            get { return listener; }
        }

        public TorrentManager Manager
        {
            get { return manager; }
        }

        public Torrent Torrent
        {
            get { return torrent; }
        }

        public EngineTestRig(string savePath)
            : this(savePath, 256 * 1024)
        {

        }

        public EngineTestRig(string savePath, int piecelength)
        {
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener);
            torrent = Torrent.Load(CreateTorrent(piecelength));
            manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            engine.Register(manager);
            //manager.Start();
        }

        public void AddConnection(IConnection connection)
        {
            listener.Add(manager, connection);
        }

        private static BEncodedDictionary CreateTorrent(int pieceLength)
        {
            BEncodedDictionary infoDict = new BEncodedDictionary();
            infoDict[new BEncodedString("piece length")] = new BEncodedNumber(pieceLength);
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

    class TestManualConnection
    {
        EngineTestRig rig1;
        EngineTestRig rig2;
        IConnection connection1a;
        IConnection connection1b;

        public TestManualConnection()
        {
            rig1 = new EngineTestRig("Downloads1");
            rig1.Manager.Start();
            rig2 = new EngineTestRig("Downloads2");
            rig2.Manager.Start();

            TcpListener socketListener = new TcpListener(1220);
            socketListener.Start();

            Socket s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, 1220);
            Socket s1b = socketListener.AcceptSocket();

            connection1a = new CustomConnection(s1a, true, "1A");
            connection1b = new CustomConnection(s1b, false, "1B");

            rig1.AddConnection(connection1a);
            rig2.AddConnection(connection1b);

            while (true)
            {
                Console.WriteLine("Connection 1A active: {0}", connection1a.Connected);
                Console.WriteLine("Connection 2A active: {0}", connection1b.Connected);
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
