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
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client.Tracker;
using System.Threading;

namespace SampleClient
{
    public class CustomTracker : Tracker
    {
        public CustomTracker(Uri uri)
            :base(uri)
        {
            this.CanScrape = false;
        }

        public override System.Threading.WaitHandle Announce(AnnounceParameters parameters)
        {
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(parameters.Id));
            return parameters.Id.WaitHandle;
        }

        public override System.Threading.WaitHandle Scrape(ScrapeParameters parameters)
        {
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, true));
            return parameters.Id.WaitHandle;
        }

        public void AddPeer(Peer p)
        {
            TrackerConnectionID id = new TrackerConnectionID(this, false, TorrentEvent.None, null);
            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
            e.Peers.Add(p);
            e.Successful = true;
            RaiseAnnounceComplete(e);
        }

        public void AddFailedPeer(Peer p)
        {
            TrackerConnectionID id = new TrackerConnectionID(this, true, TorrentEvent.None, null);
            AnnounceResponseEventArgs e = new AnnounceResponseEventArgs(id);
            e.Peers.Add(p);
            e.Successful = false;
            RaiseAnnounceComplete(e);
        }
    }

    public class NullWriter : PieceWriter
    {
        public override int Read(BufferedIO data)
        {
            data.ActualCount = data.Count;
            return data.Count;
        }

        public override void Write(BufferedIO data)
        {
        }

        public override void Close(string path, TorrentFile[] files)
        {

        }

        public override void Flush(string path, TorrentFile[] files)
        {
        }
        public override void Flush(string path, TorrentFile[] files, int pieceIndex)
        {
            
        }
    }

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

        public Uri Uri
        {
            get { return null; }
        }
    }

    public class CustomListener : PeerListener
    {
        public override void Start()
        {

        }

        public override void Stop()
        {

        }

        public CustomListener()
            :base(new IPEndPoint(IPAddress.Any, 0))
        {
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("tcp://12.123.123.1:2342"), EncryptionTypes.All);
            base.RaiseConnectionReceived(p, connection, manager);
        }
    }
    public class ConnectionPair : IDisposable
    {
        TcpListener socketListener;
        public IConnection Incoming;
        public IConnection Outgoing;

        public ConnectionPair(int port)
        {
            socketListener = new TcpListener(port);
            socketListener.Start();

            Socket s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, port);
            Socket s1b = socketListener.AcceptSocket();

            Incoming = new CustomConnection(s1a, true, "1A");
            Outgoing = new CustomConnection(s1b, false, "1B");
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }
    public class EngineTestRig
    {
        private BEncodedDictionary torrentDict;
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

        public BEncodedDictionary TorrentDict
        {
            get { return torrentDict; }
        }

        public CustomTracker Tracker
        {
            get { return (CustomTracker)this.manager.TrackerManager.CurrentTracker; }
        }


        static EngineTestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        public EngineTestRig(string savePath)
            : this(savePath, 256 * 1024, null)
        {

        }

        public EngineTestRig(string savePath, PieceWriter writer)
            : this(savePath, 256 * 1024, writer)
        {

        }

        public EngineTestRig(string savePath, int piecelength, PieceWriter writer)
        {
            if(writer == null)
                writer = new MemoryWriter(new NullWriter());
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener, writer);
            torrentDict = CreateTorrent(piecelength);
            torrent = Torrent.Load(torrentDict);
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
            announceTier.Add(new BEncodedString("custom://transfers1/announce"));
            announceTier.Add(new BEncodedString("custom://transfers2/announce"));
            announceTier.Add(new BEncodedString("http://transfers3/announce"));
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

        public TestManualConnection()
        {
            rig1 = new EngineTestRig("Downloads1");
            rig1.Manager.Start();
            rig2 = new EngineTestRig("Downloads2");
            rig2.Manager.Start();

            ConnectionPair p = new ConnectionPair(5151);

            rig1.AddConnection(p.Incoming);
            rig2.AddConnection(p.Outgoing);

            while (true)
            {
                Console.WriteLine("Connection 1A active: {0}", p.Incoming.Connected);
                Console.WriteLine("Connection 2A active: {0}", p.Outgoing.Connected);
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
