using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.Connections;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.PieceWriters;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.Client.Encryption;
using System.Threading;
namespace MonoTorrentTests
{
    public class CustomTracker : Tracker
    {
        public CustomTracker(Uri uri)
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

    public class TestWriter : PieceWriter
    {
        public override int Read(BufferedIO data)
        {
            for (int i = 0; i < data.Count; i++)
                data.Buffer.Array[data.Buffer.Offset + i] = (byte)((data.Buffer.Offset + i) % byte.MaxValue);
            data.ActualCount = data.Count;
            return data.Count;
        }

        public override void Write(BufferedIO data)
        {

        }

        public override void Close(TorrentManager manager)
        {

        }

        public override void Flush(TorrentManager manager)
        {

        }

        public override void Flush(TorrentManager manager, int pieceIndex)
        {

        }
    }

    public class CustomConnection : IConnection
    {
        public string Name;
        public event EventHandler BeginReceiveStarted;
        public event EventHandler EndReceiveStarted;

        public event EventHandler BeginSendStarted;
        public event EventHandler EndSendStarted;

        private Socket s;
        private bool incoming;

        public CustomConnection(Socket s, bool incoming)
        {
            this.s = s;
            this.incoming = incoming;
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
            if (BeginReceiveStarted != null)
                BeginReceiveStarted(null, EventArgs.Empty);
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            if (EndReceiveStarted != null)
                EndReceiveStarted(null, EventArgs.Empty);
            try
            {
                return s.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginSendStarted != null)
                BeginSendStarted(null, EventArgs.Empty);

            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            if (EndSendStarted != null)
                EndSendStarted(null, EventArgs.Empty);
            try
            {
                return s.EndSend(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }
        private bool disposed;
        public void Dispose()
        {
            disposed = true;
            s.Close();
        }

        public override string ToString()
        {
            return Name;
        }

        #region IConnection Members


        public Uri Uri
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        #endregion
    }

    public class CustomListener : ConnectionListenerBase
    {
        public override void Start()
        {

        }

        public override void Stop()
        {

        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("tcp://12.123.123.1:2342"), EncryptionTypes.All);
            base.RaiseConnectionReceived(p, connection, manager);
        }

        public override void ChangePort(int port)
        {

        }

        public override int ListenPort
        {
            get { return 0; }
        }
    }

    public class ConnectionPair : IDisposable
    {
        TcpListener socketListener;
        public CustomConnection Incoming;
        public CustomConnection Outgoing;

        public ConnectionPair(int port)
        {
            socketListener = new TcpListener(IPAddress.Loopback, port);
            socketListener.Start();

            Socket s1a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s1a.Connect(IPAddress.Loopback, port);
            Socket s1b = socketListener.AcceptSocket();

            Incoming = new CustomConnection(s1a, true);
            Outgoing = new CustomConnection(s1b, false);
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }

    public class TestRig
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


        static TestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        public TestRig(string savePath)
            : this(savePath, null)
        {

        }

        public TestRig(string savePath, PieceWriter writer)
            : this(savePath, 256 * 1024, writer)
        {

        }

        public TestRig(string savePath, int piecelength, PieceWriter writer)
        {
            if (writer == null)
                writer = new TestWriter();
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener, writer);
            torrentDict = CreateTorrent(piecelength);
            torrent = Torrent.Load(torrentDict);
            manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            engine.Register(manager);
        }

        private static void AddAnnounces(BEncodedDictionary dict)
        {
            BEncodedList announces = new BEncodedList();
            BEncodedList tier1 = new BEncodedList();
            BEncodedList tier2 = new BEncodedList();
            announces.Add(tier1);
            announces.Add(tier2);
            tier1.Add((BEncodedString)"custom://tier1/announce1");
            tier1.Add((BEncodedString)"custom://tier1/announce2");
            tier2.Add((BEncodedString)"custom://tier2/announce1");
            tier2.Add((BEncodedString)"custom://tier2/announce2");
            tier2.Add((BEncodedString)"custom://tier2/announce3");

            dict["announce"] = (BEncodedString)"custom://tier1/announce1";
            dict["announce-list"] = announces;
        }

        public void AddConnection(IConnection connection)
        {
            if (connection.IsIncoming)
                listener.Add(null, connection);
            else
                listener.Add(manager, connection);
        }

        private static BEncodedDictionary CreateTorrent(int pieceLength)
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary infoDict = new BEncodedDictionary();

            AddAnnounces(dict);
            AddMultiFiles(infoDict, pieceLength);

            dict["creation date"] = (BEncodedNumber)(int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString)"UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        private static void AddMultiFiles(BEncodedDictionary dict, int pieceLength)
        {
            BEncodedNumber[] sizes = new BEncodedNumber[] { (int)(pieceLength * 0.44), 
                                                            (int)(pieceLength * 13.25),
                                                            (int)(pieceLength * 23.68),
                                                            (int)(pieceLength * 2.05) };

            List<BEncodedList> paths = new List<BEncodedList>();
            paths.Add(new BEncodedList(new BEncodedString[] { "Dir1", "File1" }));
            paths.Add(new BEncodedList(new BEncodedString[] { "Dir1", "Dir2", "File2" }));
            paths.Add(new BEncodedList(new BEncodedString[] { "File3" }));
            paths.Add(new BEncodedList(new BEncodedString[] { "File4" }));

            BEncodedList files = new BEncodedList();
            for (int i = 0; i < paths.Count; i++)
            {
                BEncodedDictionary d = new BEncodedDictionary();
                d["path"] = paths[i];
                d["length"] = sizes[i];
                files.Add(d);
            }

            dict[new BEncodedString("files")] = files;
            dict[new BEncodedString("name")] = new BEncodedString("test.files");
            dict[new BEncodedString("piece length")] = new BEncodedNumber(pieceLength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * 25]);
        }
    }
}
