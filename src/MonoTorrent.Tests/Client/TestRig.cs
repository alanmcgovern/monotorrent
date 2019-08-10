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
using NUnit.Framework;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    public class TestWriter : PieceWriter
    {
        public List<TorrentFile> FilesThatExist = new List<TorrentFile>();
        public List<TorrentFile> DoNotReadFrom = new List<TorrentFile>();
        public bool DontWrite;
        public List<String> Paths = new List<string>();
        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (DoNotReadFrom.Contains(file))
                return 0;

            if (!Paths.Contains(file.FullPath))
                Paths.Add(file.FullPath);

            if (!DontWrite)
                for (int i = 0; i < count; i++)
                    buffer[bufferOffset + i] = (byte)(bufferOffset + i);
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {

        }

        public override void Close(TorrentFile file)
        {

        }

        public override void Flush(TorrentFile file)
        {

        }

        public override bool Exists(TorrentFile file)
        {
            return FilesThatExist.Contains(file);
        }

        public override void Move(TorrentFile file, string newPath, bool overwrite)
        {
            
        }
    }

    class CustomTracker : MonoTorrent.Client.Tracker.Tracker
    {
        public List<DateTime> AnnouncedAt = new List<DateTime>();
        public List<DateTime> ScrapedAt = new List<DateTime>();

        public bool FailAnnounce;
        public bool FailScrape;

        List<Peer> peers = new List<Peer>();

        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanAnnounce = true;
            CanScrape = true;
        }

        protected override Task<List<Peer>> DoAnnounceAsync(AnnounceParameters parameters)
        {
            AnnouncedAt.Add(DateTime.Now);
            if (FailAnnounce)
                throw new TrackerException ("Deliberately failing announce request", null);

            return Task.FromResult (peers);
        }

        protected override Task DoScrapeAsync(ScrapeParameters parameters)
        {
            ScrapedAt.Add(DateTime.Now);
            if (FailScrape)
                throw new TrackerException ("Deliberately failing scrape request", null);

            return Task.CompletedTask;
        }

        public void AddPeer(Peer peer)
        {
            peers.Add (peer);
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }

    public class CustomConnection : IConnection
    {
        public byte[] AddressBytes => ((IPEndPoint)EndPoint).Address.GetAddressBytes();
        public bool CanReconnect => false;
        public bool Connected  => Socket.Connected;
        public EndPoint EndPoint => Socket.RemoteEndPoint;
        public bool IsIncoming { get; }
        public int? ManualBytesReceived { get; set; }
        public int? ManualBytesSent { get; set; }
        public string Name => IsIncoming ? "Incoming" : "Outgoing";
        public bool SlowConnection { get; set; }
        public Uri Uri => new Uri("ipv4://127.0.0.1:1234");

        Socket Socket { get; }

        public CustomConnection(Socket socket, bool isIncoming)
        {
            Socket = socket;
            IsIncoming = isIncoming;
        }
        
        public Task ConnectAsync()
            => throw new InvalidOperationException();

        public void Dispose()
            => Socket.Close();

        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count)
        {
            if (SlowConnection)
                count = Math.Min(88, count);

            var result = await Task.Factory.FromAsync (Socket.BeginReceive(buffer, offset, count, SocketFlags.None, null, null), Socket.EndReceive);
            return ManualBytesReceived ?? result;
        }

        public async Task<int> SendAsync(byte[] buffer, int offset, int count)
        {
            if (SlowConnection)
                count = Math.Min(88, count);

            var result = await Task.Factory.FromAsync(Socket.BeginSend(buffer, offset, count, SocketFlags.None, null, null), Socket.EndSend);
            return ManualBytesSent ?? result;
        }

        public override string ToString()
            => Name;
    }

    class CustomListener : PeerListener
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
            MonoTorrent.Client.Peer p = new MonoTorrent.Client.Peer("", new Uri("ipv4://12.123.123.1:2342"), EncryptionTypes.All);
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
            socketListener.Stop();
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            socketListener.Stop();
        }
    }

    class TestRig : IDisposable
    {
        static Random Random = new Random(1000);
        static int port = 10000;
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

        public TestWriter Writer {
            get; set;
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

        public bool MetadataMode {
            get; private set;
        }

        public string MetadataPath {
            get; set;
        }

        public Torrent Torrent
        {
            get { return torrent; }
        }

        public BEncodedDictionary TorrentDict
        {
            get { return torrentDict; }
        }

        internal CustomTracker Tracker
        {
            get { return (CustomTracker)this.manager.TrackerManager.CurrentTracker; }
        }


        string savePath; int piecelength; string[][] tier;

        public void AddConnection(IConnection connection)
        {
            if (connection.IsIncoming)
                listener.Add(null, connection);
            else
                listener.Add(manager, connection);
        }
        public PeerId CreatePeer(bool processingQueue)
        {
            return CreatePeer(processingQueue, true);
        }

        public PeerId CreatePeer(bool processingQueue, bool supportsFastPeer)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 20; i++)
                sb.Append((char)Random.Next((int)'a', (int)'z'));
            Peer peer = new Peer(sb.ToString(), new Uri("ipv4://127.0.0.1:" + (port++)));
            PeerId id = new PeerId(peer, Manager);
            id.SupportsFastPeer = supportsFastPeer;
            id.ProcessingQueue = processingQueue;
            return id;
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        public async Task RecreateManager()
        {
            if (manager != null)
            {
                manager.Dispose();
                if (engine.Contains(manager))
                    await engine.Unregister(manager);
            }
            torrentDict = CreateTorrent(piecelength, files, tier);
            torrent = Torrent.Load(torrentDict);
            if (MetadataMode)
                manager = new TorrentManager(torrent.InfoHash, savePath, new TorrentSettings(), MetadataPath, new RawTrackerTiers ());
            else
                manager = new TorrentManager(torrent, savePath, new TorrentSettings());
            await engine.Register(manager);
        }

        #region Rig Creation

        TorrentFile[] files;
        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files)
            : this (savePath, piecelength, writer, trackers, files, false)
        {
            
        }

        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files, bool metadataMode)
        {
            this.files = files;
            this.savePath = savePath;
            this.piecelength = piecelength;
            this.tier = trackers;
            MetadataMode = metadataMode;
            MetadataPath = "metadataSave.torrent";
            listener = new CustomListener();
            engine = new ClientEngine(new EngineSettings(), listener, writer);
            Writer = writer;

            RecreateManager().Wait();
        }

        static TestRig()
        {
            TrackerFactory.Register("custom", uri => new CustomTracker (uri));
        }

        private static void AddAnnounces(BEncodedDictionary dict, string[][] tiers)
        {
            BEncodedList announces = new BEncodedList();
            foreach (string[] tier in tiers)
            {
                BEncodedList bTier = new BEncodedList();
                announces.Add(bTier);
                foreach (string s in tier)
                    bTier.Add((BEncodedString)s);
            }
            dict["announce"] = (BEncodedString)tiers[0][0];
            dict["announce-list"] = announces;
        }

        BEncodedDictionary CreateTorrent(int pieceLength, TorrentFile[] files, string[][] tier)
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary infoDict = new BEncodedDictionary();

            AddAnnounces(dict, tier);
            AddFiles(infoDict, files);
            if (files.Length == 1)
                dict["url-list"] = (BEncodedString)(TestWebSeed.ListenerURL + "File1.exe");
            else
                dict["url-list"] = (BEncodedString)TestWebSeed.ListenerURL;
            dict["creation date"] = (BEncodedNumber)(int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString)"UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        void AddFiles(BEncodedDictionary dict, TorrentFile[] files)
        {
            long totalSize = piecelength - 1;
            BEncodedList bFiles = new BEncodedList();
            for (int i = 0; i < files.Length; i++)
            {
                BEncodedList path = new BEncodedList();
                foreach (string s in files[i].Path.Split('/'))
                    path.Add((BEncodedString)s);
                BEncodedDictionary d = new BEncodedDictionary();
                d["path"] = path;
                d["length"] = (BEncodedNumber)files[i].Length;
                bFiles.Add(d);
                totalSize += files[i].Length;
            }

            dict[new BEncodedString("files")] = bFiles;
            dict[new BEncodedString("name")] = new BEncodedString("test.files");
            dict[new BEncodedString("piece length")] = new BEncodedNumber(piecelength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * (totalSize / piecelength)]);
        }

        public static TestRig CreateSingleFile()
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardSingleFile());
        }

        public static TestRig CreateMultiFile()
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TorrentFile[] files, int pieceLength)
        {
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files);
        }

        public static TestRig CreateTrackers(string[][] tier)
        {
            return new TestRig("", StandardPieceSize(), StandardWriter(), tier, StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TestWriter writer)
        {
            return new TestRig ("", StandardPieceSize (), writer, StandardTrackers (), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(int pieceSize)
        {
            return new TestRig("", pieceSize, StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        #region Create standard fake data

        static int StandardPieceSize()
        {
            return 256 * 1024;
        }

        static TorrentFile[] StandardMultiFile()
        {
            return new TorrentFile[] {
                new TorrentFile ("Dir1/File1", (int)(StandardPieceSize () * 0.44)),
                new TorrentFile ("Dir1/Dir2/File2", (int)(StandardPieceSize () * 13.25)),
                new TorrentFile ("File3", (int)(StandardPieceSize () * 23.68)),
                new TorrentFile ("File4", (int)(StandardPieceSize () * 2.05)),
            };
        }

        static TorrentFile[] StandardSingleFile()
        {
            return new TorrentFile[] {
                 new TorrentFile ("Dir1/File1", (int)(StandardPieceSize () * 0.44))
            };
        }

        static string[][] StandardTrackers()
        {
            return new string[][] {
                new string[] { "custom://tier1/announce1", "custom://tier1/announce2" },
                new string[] { "custom://tier2/announce1", "custom://tier2/announce2", "custom://tier2/announce3" },
            };
        }

        static TestWriter StandardWriter()
        {
            return new TestWriter();
        }

        #endregion Create standard fake data

        #endregion Rig Creation

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength)
        {
            return CreateSingleFile(torrentSize, pieceLength, false);
        }

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength, bool metadataMode)
        {
            TorrentFile[] files = StandardSingleFile();
            files[0] = new TorrentFile (files[0].Path, torrentSize);
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files, metadataMode);
        }
    }
}
