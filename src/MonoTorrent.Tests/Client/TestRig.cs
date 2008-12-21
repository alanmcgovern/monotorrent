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

namespace MonoTorrent.Client
{
    public class TestWriter : PieceWriter
    {
        public bool DontWrite;
        public List<String> Paths = new List<string>();
        public override int Read(BufferedIO data)
        {
            long idx = data.Offset;
            for (int i = 0; i < data.Files.Length; i++)
            {
                if (idx < data.Files[i].Length)
                {
                    string path = System.IO.Path.Combine(data.Path, data.Files[i].Path);
                    if (!Paths.Contains(path))
                        Paths.Add(path);
                    break;
                }
                else
                {
                    idx -= data.Files[i].Length;
                }
            }
             
            data.ActualCount = data.Count;
            if (DontWrite)
                return data.Count;

            for (int i = 0; i < data.Count; i++)
                data.Buffer.Array[data.Buffer.Offset + i] = (byte)(data.Buffer.Offset + i);
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

    public class CustomTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public List<DateTime> AnnouncedAt = new List<DateTime>();
        public List<DateTime> ScrapedAt = new List<DateTime>();

        public CustomTracker(Uri uri)
            : base(uri)
        {
            this.CanScrape = false;
        }

        public override System.Threading.WaitHandle Announce(AnnounceParameters parameters)
        {
            AnnouncedAt.Add(DateTime.Now);
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(parameters.Id));
            return parameters.Id.WaitHandle;
        }

        public override System.Threading.WaitHandle Scrape(ScrapeParameters parameters)
        {
            ScrapedAt.Add(DateTime.Now);
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
        //private bool disposed;
        public void Dispose()
        {
           // disposed = true;
            s.Close();
        }

        public override string ToString()
        {
            return Name;
        }

        #region IConnection Members


        public Uri Uri
        {
            get { return new Uri("tcp://127.0.0.1:1234"); }
        }

        #endregion
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

    public class TestRig : IDisposable
    {
        private BEncodedDictionary torrentDict;
        private ClientEngine engine;
        private CustomListener listener;
        private TorrentManager manager;
        private Torrent torrent;

        public int BlocksPerPiece
        {
            get { return torrent.PieceLength / (16 * 1024); }
        }

        public int Pieces
        {
            get { return torrent.Pieces.Count; }
        }

        public int TotalBlocks
        {
            get
            {
                int count = 0;
                long size = torrent.Size;
                while (size > 0)
                {
                    count++;
                    size -= Piece.BlockSize;
                }
                return count;
            }
        }

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

        public TestRig(bool singleFile)
            : this("", singleFile)
        {

        }

        public TestRig(string savePath)
            : this(savePath, false)
        {

        }

        public TestRig(string savePath, bool singleFile)
            : this(savePath, null, singleFile)
        {

        }

        public TestRig(string savePath, PieceWriter writer)
            : this(savePath, 256 * 1024, writer, false)
        {

        }
        public TestRig(string savePath, PieceWriter writer, bool singleFile)
            : this(savePath, 256 * 1024, writer, singleFile)
        {

        }

        public TestRig(string savePath, int piecelength, PieceWriter writer)
            :this(savePath, piecelength, writer, false)
        {

        }

        public TestRig(string savePath, int piecelength, PieceWriter writer, bool singleFile)
        {
            if (writer == null)
                writer = new TestWriter();
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener, writer);
            torrentDict = CreateTorrent(piecelength, singleFile);
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

        private static BEncodedDictionary CreateTorrent(int pieceLength, bool singleFile)
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary infoDict = new BEncodedDictionary();

            AddAnnounces(dict);
            if (singleFile)
                AddSingleFile(infoDict, pieceLength);
            else
                AddMultiFiles(infoDict, pieceLength);
            if (singleFile)
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce/File1.exe";
            else
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce";
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
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * 40]);
        }


        private static void AddSingleFile(BEncodedDictionary dict, int pieceLength)
        {
            BEncodedNumber[] sizes = new BEncodedNumber[] { (int)(pieceLength * 0.44) + 
                                                            (int)(pieceLength * 13.25)+
                                                            (int)(pieceLength * 23.68)+
                                                            (int)(pieceLength * 2.05) };

            List<BEncodedList> paths = new List<BEncodedList>();
            paths.Add(new BEncodedList(new BEncodedString[] { "Dir1", "File1" }));

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
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * 40]);
            dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce/File1.exe";
        }

        #region IDisposable Members

        public void Dispose()
        {
            engine.Dispose();
        }


        public static TestRig CreateSingleFile()
        {
            return new TestRig(true);
        }

        public  static TestRig CreateMultiFile()
        {
            return new TestRig(false);
        }

        #endregion
    }
}
